using Caliburn.Micro;
using RoiEditor.Core;
using RoiEditor.Core.Attributes;
using RoiEditor.Core.Interaction;
using RoiEditor.Core.IO;
using RoiEditor.Core.Memory;
using RoiEditor.Core.Rendering;
using RoiEditor.Enums;
using RoiEditor.Events;
using RoiEditor.Models;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace RoiEditor.Controls
{
    /// <summary>
    /// RoiEditorCanvas（单层稳定版，含 token）
    /// - World Space = Level0 像素坐标
    /// - 瓦片：MapCanvas（WorldContainer 下）
    /// - 未选中 ROI：StaticLayer（WorldContainer 下）
    /// - 选中 ROI：EditorLayer（Screen Space）
    /// </summary>
    public partial class RoiEditorCanvas : UserControl
    {
        private const double MIN_ZOOM = 0.01;
        private const double MAX_ZOOM = 200.0;

        private const int TILE_SIZE = 512;

        // =========================
        // Dependency Properties
        // =========================
        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource), typeof(IObservableCollection<ROIRegion>), typeof(RoiEditorCanvas),
            new PropertyMetadata(null, OnItemsSourceChanged));

        public static readonly DependencyProperty ROIOperationModeProperty = DependencyProperty.Register(
            nameof(ROIOperationMode), typeof(ROIOperationMode), typeof(RoiEditorCanvas),
            new PropertyMetadata(ROIOperationMode.ROI_OS_Pan, OnROIOperationModeChanged));

        public static readonly DependencyProperty MapPathProperty = DependencyProperty.Register(
            nameof(MapPath), typeof(string), typeof(RoiEditorCanvas),
            new PropertyMetadata(null, OnMapPathChanged));

        public static readonly DependencyProperty SelectedROIRegionProperty = DependencyProperty.Register(
            nameof(SelectedROIRegion), typeof(ROIRegion), typeof(RoiEditorCanvas),
            new PropertyMetadata(null, OnSelectedROIRegionChanged));

        public static readonly DependencyProperty EventAggregatorProperty = DependencyProperty.Register(
            nameof(EventAggregator), typeof(IEventAggregator), typeof(RoiEditorCanvas),
            new PropertyMetadata(null, OnEventAggregatorChanged));

        public static readonly DependencyProperty CurrentLevelProperty = DependencyProperty.Register(
            nameof(CurrentLevel),
            typeof(int),
            typeof(RoiEditorCanvas),
            new FrameworkPropertyMetadata(0) { BindsTwoWayByDefault = true });

        public static readonly DependencyProperty MaxLevelProperty = DependencyProperty.Register(
            nameof(MaxLevel),
            typeof(int),
            typeof(RoiEditorCanvas),
            new FrameworkPropertyMetadata(0) { BindsTwoWayByDefault = true });

        public static readonly DependencyProperty ActiveROIProperty = DependencyProperty.Register(
            nameof(ActiveROI), typeof(ROI), typeof(RoiEditorCanvas),
            new PropertyMetadata(null));

        public ROI ActiveROI
        {
            get => (ROI)GetValue(ActiveROIProperty);
            set => SetValue(ActiveROIProperty, value);
        }

        public int MaxLevel
        {
            get => (int)GetValue(MaxLevelProperty);
            set => SetValue(MaxLevelProperty, value);
        }

        public int CurrentLevel
        {
            get => (int)GetValue(CurrentLevelProperty);
            set => SetValue(CurrentLevelProperty, value);
        }

        public IObservableCollection<ROIRegion> ItemsSource
        {
            get => (IObservableCollection<ROIRegion>)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public ROIOperationMode ROIOperationMode
        {
            get => (ROIOperationMode)GetValue(ROIOperationModeProperty);
            set => SetValue(ROIOperationModeProperty, value);
        }

        public string MapPath
        {
            get => (string)GetValue(MapPathProperty);
            set => SetValue(MapPathProperty, value);
        }

        public ROIRegion SelectedROIRegion
        {
            get => (ROIRegion)GetValue(SelectedROIRegionProperty);
            set => SetValue(SelectedROIRegionProperty, value);
        }

        public IEventAggregator EventAggregator
        {
            get => (IEventAggregator)GetValue(EventAggregatorProperty);
            set => SetValue(EventAggregatorProperty, value);
        }

        // =========================
        // Internal State
        // =========================
        private readonly Dictionary<string, Image> _visibleTiles = new Dictionary<string, Image>();
        private readonly HashSet<string> _loadingTiles = new HashSet<string>();
        private readonly Dictionary<string, CancellationTokenSource> _loadingCts = new Dictionary<string, CancellationTokenSource>();

        private bool _isCleanedUp;

        private int _mapVersion = 0;

        private VisualHost _staticHost;
        private VisualHost _editorHost;

        private ROIRegion _hoverROIRegion;
        private ROIRegion _activeROIRegion;

        private readonly List<ROIRegion> _hitTestCache = new List<ROIRegion>();

        private DispatcherTimer _debounceTimer;

        private readonly TileLoader _tileLoader = new TileLoader();
        private readonly RoiRenderer _renderer = new RoiRenderer();
        private TilePool _tilePool;
        private readonly MapService _mapService = new MapService();

        private QuadTree<ROIRegion> _spatialIndex;

        private IInteractionTool _currentTool;
        private Dictionary<ROIOperationMode, IInteractionTool> _tools;

        // =========================
        // Initialization
        // =========================
        public RoiEditorCanvas()
        {
            InitializeComponent();

            InitializeRenderLayers();

            _tilePool = new TilePool(MapCanvas, initialCount: 50);

            MouseWheel += OnMouseWheelZoom;
            SizeChanged += (s, e) => UpdateTiles();
            MouseLeave += (s, e) =>
            {
                if (_hoverROIRegion != null)
                {
                    _hoverROIRegion = null;
                    RenderStaticLayer();
                }
            };

            Unloaded += OnUnloaded;

            _debounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer.Stop();
                UpdateTiles();
            };

            _tools = new Dictionary<ROIOperationMode, IInteractionTool>();

            // 1. 获取当前程序集 (或者包含 Tool 的特定程序集)
            var assembly = Assembly.GetExecutingAssembly();

            // 2. 找到所有实现了 IInteractionTool 接口 且 带有 [RoiTool] 特性的类
            var toolTypes = assembly.GetTypes()
                .Where(t => typeof(IInteractionTool).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .Where(t => t.GetCustomAttribute<RoiToolAttribute>() != null);

            // 3. 遍历并实例化
            foreach (var type in toolTypes)
            {
                // 读取特性里的 Enum 值
                var attribute = type.GetCustomAttribute<RoiToolAttribute>();

                // 创建实例：Activator.CreateInstance(类型, 构造函数参数...)
                // 这里把 'this' (也就是当前 Canvas) 传给 Tool 的构造函数
                var toolInstance = (IInteractionTool)Activator.CreateInstance(type, this);

                // 添加到字典
                if (!_tools.ContainsKey(attribute.Mode))
                {
                    _tools.Add(attribute.Mode, toolInstance);
                }
            }

            _currentTool = _tools[ROIOperationMode.ROI_OS_Pan];
            _currentTool.Activate();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Cleanup();
        }

        private void InitializeRenderLayers()
        {
            _staticHost = new VisualHost();
            StaticLayer.Children.Add(_staticHost);

            _editorHost = new VisualHost();
            EditorLayer.Children.Add(_editorHost);
        }

        // =========================
        // DP callbacks
        // =========================
        private static void OnMapPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as RoiEditorCanvas)?.ReloadMap(e.NewValue as string);
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var c = (RoiEditorCanvas)d;
            
            if (e.OldValue is INotifyCollectionChanged oldColl)
                oldColl.CollectionChanged -= c.OnCollectionChanged;

            if (e.NewValue is INotifyCollectionChanged newColl)
                newColl.CollectionChanged += c.OnCollectionChanged;

            c.RebuildSpatialIndex();
            c.RenderStaticLayer();
            c.RenderEditorLayer();
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {           
            RebuildSpatialIndex();
            RenderStaticLayer();
            RenderEditorLayer();
        }

        private static void OnSelectedROIRegionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var c = (RoiEditorCanvas)d;
            c.ApplyExternalSelection(e.NewValue as ROIRegion);
        }

        private static void OnEventAggregatorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var c = (RoiEditorCanvas)d;
            if (e.OldValue is IEventAggregator oldEa) oldEa.Unsubscribe(c);
            if (e.NewValue is IEventAggregator newEa) newEa.Subscribe(c);
        }

        // =========================
        // Public API
        // =========================
        public void ReloadMap(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!System.IO.Directory.Exists(path)) return;

            _mapService.LoadMap(path);
            MaxLevel = _mapService.MaxLevel;

            Interlocked.Increment(ref _mapVersion);

            _tileLoader.ClearCache();
            CancelAllLoading();

            // 回收显示瓦片
            foreach (var img in _visibleTiles.Values.ToList())
                _tilePool.Return(img);

            _visibleTiles.Clear();
            _loadingTiles.Clear();

            // ROIRegion 空间索引：世界尺寸会变化
            RebuildSpatialIndex();

            // 初始化视图：默认从最粗层开始（避免一上来冲进 Level0 高清）
            double initialScale = 1.0 / Math.Pow(2, _mapService.MaxLevel);
            MainMatrix.Matrix = new Matrix(initialScale, 0, 0, initialScale, 0, 0);

            // 如果控件已布局完成，则 Fit；否则等 Loaded 后 Fit
            if (IsLoaded && ActualWidth > 1 && ActualHeight > 1)
                FitToCoarsestAndCenter();
            else
                RequestFitAfterLayout();
        }

        private void CancelAllLoading()
        {
            foreach (var cts in _loadingCts.Values)
            {
                try { cts.Cancel(); } catch { }
                try { cts.Dispose(); } catch { }
            }
            _loadingCts.Clear();
            _loadingTiles.Clear();
        }

        // =========================
        // Tile Engine (single-layer)
        // =========================
        internal void UpdateTiles()
        {
            if (string.IsNullOrEmpty(_mapService.MapPath)) return;
            if (ActualWidth <= 1 || ActualHeight <= 1) return;

            Matrix m = MainMatrix.Matrix;

            // world = level0 像素：scale 越小，level 越大（越粗）
            int targetLevel = (int)Math.Max(0, Math.Log(1.0 / Math.Max(1e-9, m.M11), 2));
            if (targetLevel > _mapService.MaxLevel) targetLevel = _mapService.MaxLevel;
            if (targetLevel < 0) targetLevel = 0;

            if (CurrentLevel != targetLevel)
                CurrentLevel = targetLevel;

            // factor: level 的世界放大因子（瓦片在世界中的尺寸 = 512 * 2^level）
            double factor = Math.Pow(2, targetLevel);

            if (!m.HasInverse) return;
            m.Invert();

            Rect viewport = new Rect(
                m.Transform(new Point(0, 0)),
                m.Transform(new Point(ActualWidth, ActualHeight)));

            double tileWorld = TILE_SIZE * factor;

            int startCol = (int)(viewport.X / tileWorld);
            int startRow = (int)(viewport.Y / tileWorld);
            int endCol = (int)((viewport.X + viewport.Width) / tileWorld) + 1;
            int endRow = (int)((viewport.Y + viewport.Height) / tileWorld) + 1;

            if (_mapService.GetLevelBounds(targetLevel, out int maxRow, out int maxCol))
            {
                startRow = Math.Max(0, startRow);
                startCol = Math.Max(0, startCol);
                endRow = Math.Min(maxRow + 1, endRow);
                endCol = Math.Min(maxCol + 1, endCol);
            }
            else
            {
                return;
            }

            var needed = new HashSet<string>();
            for (int r = startRow; r < endRow; r++)
                for (int c = startCol; c < endCol; c++)
                    needed.Add($"{targetLevel}_{r}_{c}");

            // 差量取消：把不需要的 in-flight 任务取消掉
            var keysToCancel = _loadingCts.Keys.Where(k => !needed.Contains(k)).ToList();
            foreach (var k in keysToCancel)
            {
                if (_loadingCts.TryGetValue(k, out var cts))
                {
                    try { cts.Cancel(); } catch { }
                    try { cts.Dispose(); } catch { }
                }
                _loadingCts.Remove(k);
                _loadingTiles.Remove(k);
            }

            // 回收不再需要的可见瓦片
            var toRemove = _visibleTiles.Keys.Where(k => !needed.Contains(k)).ToList();
            foreach (var k in toRemove)
            {
                var img = _visibleTiles[k];
                _tilePool.Return(img);
                _visibleTiles.Remove(k);
            }

            // 加载新增瓦片（中心优先调度）
            int centerRow = (startRow + endRow - 1) / 2;
            int centerCol = (startCol + endCol - 1) / 2;

            // 先把需要加载的 key 收集起来
            var loadList = new List<string>(needed.Count);
            foreach (var k in needed)
            {
                if (_visibleTiles.ContainsKey(k)) continue;
                if (_loadingTiles.Contains(k)) continue;
                loadList.Add(k);
            }

            // 按“离中心的瓦片距离”排序：优先中心
            loadList.Sort((a, b) =>
            {
                // key: "level_row_col"
                var pa = a.Split('_');
                int ra = int.Parse(pa[1]);
                int ca = int.Parse(pa[2]);

                var pb = b.Split('_');
                int rb = int.Parse(pb[1]);
                int cb = int.Parse(pb[2]);

                int da = Math.Abs(ra - centerRow) + Math.Abs(ca - centerCol);
                int db = Math.Abs(rb - centerRow) + Math.Abs(cb - centerCol);

                return da.CompareTo(db);
            });

            // 按排序结果发起加载
            for (int i = 0; i < loadList.Count; i++)
            {
                var k = loadList[i];

                var p = k.Split('_');
                int lvl = int.Parse(p[0]);
                int r = int.Parse(p[1]);
                int c = int.Parse(p[2]);

                string tilePath = _mapService.GetTilePath(lvl, r, c);
                double x = c * tileWorld;
                double y = r * tileWorld;

                var cts = new CancellationTokenSource();
                _loadingCts[k] = cts;
                _loadingTiles.Add(k);

                _ = LoadTileAsync(k, tilePath, x, y, tileWorld, _mapVersion,targetLevel, cts.Token);
            }

        }

        private async Task LoadTileAsync(string key, string path, double x, double y,
            double tileWorld, 
            int mapVersion, 
            int expectedLevel,
            CancellationToken token)
        {
            if (string.IsNullOrEmpty(key)) return;

            int tileLevel = -1;
            int idx = key.IndexOf('_');
            if (idx > 0) int.TryParse(key.Substring(0, idx), out tileLevel);

            try
            {

                if (token.IsCancellationRequested)
                {
                    System.Diagnostics.Debug.WriteLine($"[Tile] CANCELED before load {key}");
                    return;
                }

                // 真正支持取消的加载
                var imgSource = await _tileLoader.LoadAsync(path, token).ConfigureAwait(false);
                if (token.IsCancellationRequested)
                {
                    System.Diagnostics.Debug.WriteLine($"[Tile] CANCELED before load {key}");
                    return;
                }
                if (imgSource == null) return;

                //=== UI提交：所有UI与集合操作只在UI线程做 ===
                await Dispatcher.InvokeAsync(() =>
                {
                    if (_isCleanedUp) return;
                    if (token.IsCancellationRequested) return;

                    //如果该key已经不在in-flight（被UpdateTiles取消并移除了）,直接丢弃
                    if (!_loadingCts.ContainsKey(key) || !_loadingTiles.Contains(key))
                        return;

                    //防切图跨版本
                    if (mapVersion != _mapVersion) return;

                    //层级一致性：防“僵尸瓦片”
                    //这里用expectedLevel（调度时的目标层级），并在UI线程比较CurrentLevel(最新)
                    if(tileLevel != -1 && tileLevel != expectedLevel) return;
                    if (CurrentLevel != expectedLevel) return;

                    var img = _tilePool.Rent();
                    if (img == null) return;

                    img.Source = imgSource;
                    // 缝隙修复：轻微 overlap
                    double overlap = 1.0;
                    img.Width = tileWorld + overlap;
                    img.Height = tileWorld + overlap;
                    img.Visibility = Visibility.Visible;

                    Canvas.SetLeft(img,x);
                    Canvas.SetTop(img,y);

                    if(_visibleTiles.TryGetValue(key,out var old))
                        _tilePool.Return(old);

                    _visibleTiles[key] = img;
                },DispatcherPriority.Render);
            }
            catch (OperationCanceledException)
            {
                // expected
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadTileAsync Error] {ex.Message}");
            }
            finally
            {
                try
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if(_isCleanedUp) return;

                        _loadingTiles.Remove(key);

                        if(_loadingCts.TryGetValue(key,out var cts))
                        {
                            _loadingCts.Remove(key);
                            try { cts.Dispose(); } catch { }
                        }
                    },DispatcherPriority.Render);
                }
                catch (Exception ex) 
                {
                    System.Diagnostics.Debug.WriteLine($"[LoadTileAsync finally clear Error] {ex.Message}");
                }
            }
        }

        // =========================
        // Rendering (ROI)
        // =========================
        private void RenderStaticLayer()
        {
            if (ItemsSource == null)
            {
                _staticHost.SetVisual(null);
                return;
            }

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                _renderer.DrawStaticLayer(dc, ItemsSource, _hoverROIRegion, MainMatrix.Matrix.M11);
            }

            _staticHost.SetVisual(visual);
        }

        private void RenderEditorLayer()
        {
            var visual = new DrawingVisual();
            if (_activeROIRegion != null)
            {
                using (var dc = visual.RenderOpen())
                {
                    _renderer.DrawEditorLayer(dc, _activeROIRegion, MainMatrix.Matrix);
                }
            }
            _editorHost.SetVisual(visual);
        }

        // =========================
        // Interaction
        // =========================
        private static void OnROIOperationModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RoiEditorCanvas canvas)
                canvas.SwitchTool((ROIOperationMode)e.NewValue);
        }

        private void SwitchTool(ROIOperationMode newMode)
        {
            if (_tools == null) return;

            // 1. 尝试从字典里拿出工具 (不管是 Pen 还是 FitToScreen，都在字典里)
            if (_tools.TryGetValue(newMode, out var newTool))
            {
                // 2. 【通用逻辑】判断工具类型
                if (newTool.IsActionOnly)
                {
                    // A. 如果是瞬时动作 (Action)
                    // 直接激活执行逻辑，执行完就拉倒
                    // 不替换 _currentTool，不影响当前状态
                    newTool.Activate();
                }
                else
                {
                    // B. 如果是长效工具 (State)
                    if (_currentTool == newTool) return;

                    _currentTool?.Deactivate();
                    _currentTool = newTool;
                    _currentTool.Activate();

                    // 同步配置页
                    //CurrentSettings = _currentTool.SettingsViewModel;
                }
            }
        }

        #region MouseAction
        private void OnMouseWheelZoom(object sender, MouseWheelEventArgs e)
        {
            var pos = e.GetPosition(this);

            Matrix m = MainMatrix.Matrix;
            double scaleFactor = 1.1;
            bool zoomingOut = e.Delta < 0;
            double scale = e.Delta > 0 ? scaleFactor : (1.0 / scaleFactor);

            double nextScale = m.M11 * scale;
            if (nextScale > MAX_ZOOM || nextScale < MIN_ZOOM) return;

            m.ScaleAt(scale, scale, pos.X, pos.Y);
            MainMatrix.Matrix = m;

            // 计算新层级（和 UpdateTiles 里的公式一致）
            int newLevel = (int)Math.Max(0, Math.Log(1.0 / Math.Max(1e-9, m.M11), 2));
            if (newLevel > _mapService.MaxLevel) newLevel = _mapService.MaxLevel;
            if (newLevel < 0) newLevel = 0;

            // 关键：缩小或跨层级时立刻更新（不等 debounce）
            if (zoomingOut || newLevel != CurrentLevel)
                UpdateTiles();
            else
                UpdateTilesWithDebounce();

            RenderStaticLayer();
            RenderEditorLayer();
        }


        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            Focus();
            _currentTool.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            // 如果容器是显示的，说明当前正在用自定义光标
            // 必须先移动光标 UI，再把事件传给 Tool
            if (CursorContainer.Visibility == Visibility.Visible)
            {
                // 获取鼠标相对于 Canvas 的坐标
                var pos = e.GetPosition(this);
                // 调用上面的位移方法
                UpdateCursorUI(pos);
            }
            _currentTool.OnMouseMove(e);
        }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) => _currentTool.OnMouseUp(e);
        protected override void OnKeyDown(KeyEventArgs e) => _currentTool.OnKeyDown(e);

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);

            // 1. 强制隐藏自定义光标容器 (红十字消失)
            if (CursorContainer.Visibility == Visibility.Visible)
            {
                CursorContainer.Visibility = Visibility.Collapsed;
            }

            // 2. 强制恢复系统箭头 (让用户能看到鼠标去点别的地方)
            // 注意：这里必须显式设为 Arrow，不能设为 null，否则可能还是 None
            this.Cursor = Cursors.Arrow;
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            base.OnMouseEnter(e);

            // 1. 如果没有工具，啥都不做
            if (_currentTool == null) return;

            // 2. 重新询问当前工具的光标策略
            // 这就是策略模式的好处！我们不需要手动记刚才是什么状态，直接问工具就行。
            var customView = (_currentTool as ToolBase)?.GetCustomCursorView();

            if (customView != null)
            {
                // 如果工具想要自定义光标 (比如 CreateRectTool)
                // 这行代码会：隐藏系统鼠标 -> 显示 CursorContainer -> 更新位置
                SetCustomCursor(customView);
            }
            else
            {
                // 如果工具想要系统光标 (比如 SelectTool 的 Hand)
                // 这行代码会：隐藏 CursorContainer -> 设置 this.Cursor
                SetSystemCursor((_currentTool as ToolBase)?.SystemCursor);
            }
        }

        #endregion

        /// <summary>
        /// 立即刷新：用于Zoom跨层，Fit，Resize等必须立刻更新瓦片的场景
        /// </summary>
        internal void RefreshTilesImmediate()
        {
            UpdateTiles();
            RenderEditorLayer();
        }

        internal void RedrawEditorLayer() => RenderEditorLayer();

        /// <summary>
        /// Pan专用：每帧只重画EditorLayer，并让瓦片更新走debounce（避免疯狂调度）
        /// </summary>
        internal void PanRefresh()
        {
            RenderEditorLayer();
            UpdateTilesWithDebounce();
        }

        internal void SetHoverROIRegion(ROIRegion item)
        {
            if (_hoverROIRegion != item)
            {
                _hoverROIRegion = item;
                RenderStaticLayer();
            }
        }

        internal void SelectROIRegion(ROIRegion item)
        {
            SetCurrentValue(SelectedROIRegionProperty, item);
        }

        // =========================
        // Spatial Index & HitTest
        // =========================
        private Rect GetROIRegionBounds(ROIRegion item)
        {
            if (item == null || item.Points == null || item.Points.Count == 0)
                return Rect.Empty;

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (var p in item.Points)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }

            return new Rect(minX, minY, Math.Max(0, maxX - minX), Math.Max(0, maxY - minY));
        }

        internal void RebuildSpatialIndex()
        {
            if (ItemsSource == null || ItemsSource.Count == 0)
            {
                _spatialIndex = null;
                return;
            }

            // 地图未加载时也允许建立一个保守索引（避免 maxlevel==0 的误判）
            double worldW = 100000;
            double worldH = 100000;

            if (!string.IsNullOrEmpty(_mapService.MapPath) && _mapService.GetWorldSize(out double w, out double h))
            {
                worldW = w;
                worldH = h;
            }

            _spatialIndex = new QuadTree<ROIRegion>(
                new Rect(0, 0, worldW * 1.5, worldH * 1.5),
                GetROIRegionBounds,
                maxObjects: 20,
                maxLevels: 8);

            foreach (var item in ItemsSource)
            {
                if (item?.Points != null && item.Points.Count >= 3)
                    _spatialIndex.Insert(item);
            }
        }

        internal ROIRegion HitTestROIRegion(Point wPos)
        {
            if (_spatialIndex == null)
                return HitTestROIRegionLegacy(wPos);

            _hitTestCache.Clear();
            _spatialIndex.Query(wPos, _hitTestCache);
            if (_hitTestCache.Count == 0) return null;

            ROIRegion bestHit = null;
            int bestIndex = -1;

            foreach (var item in _hitTestCache)
            {
                if (IsPointInROIRegion(item, wPos))
                {
                    int index = ItemsSource.IndexOf(item);
                    if (index > bestIndex)
                    {
                        bestIndex = index;
                        bestHit = item;
                    }
                }
            }

            return bestHit;
        }

        #region Cursor

        internal void SetCustomCursor(UIElement view)
        {
            if (view == null)
            {
                // 如果传进来是 null，说明要恢复系统光标
                SetSystemCursor(Cursors.Arrow);
                return;
            }

            // 1. 隐藏系统鼠标
            this.Cursor = Cursors.None;

            // 2. 把工具传进来的 UI 塞到容器里
            CursorContainer.Content = view;

            // 3. 显示容器
            CursorContainer.Visibility = Visibility.Visible;

            // 4. 立即同步位置
            var p = Mouse.GetPosition(this);
            UpdateCursorUI(p);
        }

        // 【通用方法】恢复系统光标
        internal void SetSystemCursor(Cursor cursor)
        {
            CursorContainer.Visibility = Visibility.Collapsed;
            CursorContainer.Content = null; // 清空内容
            this.Cursor = cursor ?? Cursors.Arrow;
        }

        // 【方法定义】
        // p 是鼠标在 Canvas 上的坐标 (Point)
        private void UpdateCursorUI(Point p)
        {
            // CursorTransform 是你在 XAML 里给 TranslateTransform 起的名字
            // 通过修改它的 X, Y，显卡会直接位移 UI，不触发重新布局，性能最高
            CursorTransform.X = p.X;
            CursorTransform.Y = p.Y;
        }

        #endregion

        private bool IsPointInROIRegion(ROIRegion roi, Point p)
        {
            var geom = RoiRenderer.BuildGeometry(roi);
            if (geom.FillContains(p)) return true;

            double strokeWidth = 6.0 / Math.Max(1e-6, MainMatrix.Matrix.M11);
            var pen = new Pen(Brushes.Transparent, strokeWidth);
            if (geom.StrokeContains(pen, p)) return true;

            return false;
        }

        private ROIRegion HitTestROIRegionLegacy(Point wPos)
        {
            if (ItemsSource == null) return null;

            for (int i = ItemsSource.Count - 1; i >= 0; i--)
            {
                var roi = ItemsSource[i];
                if (roi?.Points == null || roi.Points.Count < 3) continue;

                var geom = RoiRenderer.BuildGeometry(roi);
                if (!geom.Bounds.Contains(wPos)) continue;

                if (geom.FillContains(wPos)) return roi;

                var pen = new Pen(Brushes.Transparent, 6.0 / Math.Max(1e-6, MainMatrix.Matrix.M11));
                if (geom.StrokeContains(pen, wPos)) return roi;
            }

            return null;
        }

        private void ApplyExternalSelection(ROIRegion newSelection)
        {
            if (ReferenceEquals(_activeROIRegion, newSelection))
                return;

            if (_activeROIRegion != null) _activeROIRegion.IsSelected = false;

            _activeROIRegion = newSelection;

            if (_activeROIRegion != null) _activeROIRegion.IsSelected = true;

            _hoverROIRegion = null;

            RenderStaticLayer();
            RenderEditorLayer();
        }

        // =========================
        // Fit-to-coarsest (world = level0)
        // =========================

        internal void FitToCoarsestAndCenter()
        {
            if (string.IsNullOrEmpty(_mapService.MapPath))
                return;

            if (!_mapService.GetWorldSize(out double mapW, out double mapH))
                return;

            if (mapW <= 1 || mapH <= 1 || ActualWidth <= 1 || ActualHeight <= 1)
                return;

            double scaleFit = Math.Min(ActualWidth / mapW, ActualHeight / mapH);
            double scale = scaleFit;

            if (scale > MAX_ZOOM) scale = MAX_ZOOM;
            if (scale < MIN_ZOOM) scale = MIN_ZOOM;

            double tx = (ActualWidth * 0.5) - (mapW * 0.5) * scale;
            double ty = 0;

            MainMatrix.Matrix = new Matrix(scale, 0, 0, scale, tx, ty);

            UpdateTiles();
            RenderStaticLayer();
            RenderEditorLayer();
        }

        private void RequestFitAfterLayout()
        {
            if (!IsLoaded || ActualWidth <= 1 || ActualHeight <= 1)
            {
                RoutedEventHandler handler = null;
                handler = (s, e) =>
                {
                    Loaded -= handler;
                    Dispatcher.BeginInvoke(new System.Action(FitToCoarsestAndCenter), DispatcherPriority.Loaded);
                };
                Loaded += handler;
                return;
            }

            Dispatcher.BeginInvoke(new System.Action(FitToCoarsestAndCenter), DispatcherPriority.Loaded);
        }

        private void UpdateTilesWithDebounce()
        {
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }


        public void Cleanup()
        {
            if (_isCleanedUp) return;
            _isCleanedUp = true;

            // 0) 提升版本号：让任何“旧版本回来的 await”直接失效
            System.Threading.Interlocked.Increment(ref _mapVersion);

            // 1) 停止 debounce timer
            if (_debounceTimer != null)
            {
                try { _debounceTimer.Stop(); } catch { }
                _debounceTimer = null;
            }

            // 2) 停止交互捕获（避免鼠标被控件“咬住”）
            try { ReleaseMouseCapture(); } catch { }

            // 3) 取消并释放所有在途加载
            try
            {
                foreach (var cts in _loadingCts.Values.ToList())
                {
                    try { cts.Cancel(); } catch { }
                    try { cts.Dispose(); } catch { }
                }
                _loadingCts.Clear();
            }
            catch { }

            // 4) 清理加载状态
            try { _loadingTiles.Clear(); } catch { }

            // 5) 归还所有可见瓦片到对象池
            try
            {
                foreach (var img in _visibleTiles.Values.ToList())
                {
                    try { _tilePool.Return(img); } catch { }
                }
                _visibleTiles.Clear();
            }
            catch { }

            // 6) 清空 visual（避免 VisualHost 持有 DrawingVisual）
            try { _staticHost?.SetVisual(null); } catch { }
            try { _editorHost?.SetVisual(null); } catch { }

            // 7) 清空运行态引用，帮助 GC
            _hoverROIRegion = null;
            _activeROIRegion = null;
            _spatialIndex = null;
            _hitTestCache.Clear();

            // 8) EventAggregator 退订，防止控件被外部引用住
            try { EventAggregator?.Unsubscribe(this); } catch { }
        }

    }
}
