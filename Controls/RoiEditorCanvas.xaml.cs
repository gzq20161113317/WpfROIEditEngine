using Caliburn.Micro;
using RoiEditor.Core;
using RoiEditor.Core.Attributes;
using RoiEditor.Core.Helpers;
using RoiEditor.Core.Interaction;
using RoiEditor.Core.IO;
using RoiEditor.Core.Memory;
using RoiEditor.Core.Rendering;
using RoiEditor.Enums;
using RoiEditor.Events;
using RoiEditor.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        public static readonly DependencyProperty SelectedRegionsProperty = DependencyProperty.Register(
            nameof(SelectedRegions),
            typeof(ObservableCollection<ROIRegion>), // 强类型，方便 Tool 使用
            typeof(RoiEditorCanvas),
            new FrameworkPropertyMetadata(null, (d, e) =>
            {
                var canvas = d as RoiEditorCanvas;
                // 当 ViewModel 绑定的列表传进来时，View 自动重绘
                if (e.OldValue is ObservableCollection<ROIRegion> oldList)
                {
                    oldList.CollectionChanged -= canvas.OnSelectedRegionsCollectionChanged;
                }
                if (e.NewValue is ObservableCollection<ROIRegion> newList)
                {
                    newList.CollectionChanged += canvas.OnSelectedRegionsCollectionChanged;
                }
                // 列表对象都换了，肯定要重画
                canvas.RenderEditorLayer();
            }));

        // 2. 属性包装器 (保持名称不变，这样 SelectROIRegionTool 不需要改代码)
        public ObservableCollection<ROIRegion> SelectedRegions
        {
            get => (ObservableCollection<ROIRegion>)GetValue(SelectedRegionsProperty);
            set => SetValue(SelectedRegionsProperty, value);
        }

        // 定义一个锁，用于区分“内部设置”还是“外部设置”
        private bool _isSettingMapBoundsInternal = false;

        public static readonly DependencyProperty ActualMapBoundsProperty = DependencyProperty.Register(
            nameof(ActualMapBounds), typeof(Rect), typeof(RoiEditorCanvas),
            new FrameworkPropertyMetadata(
                Rect.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnActualMapBoundsChanged)); // 注册回调

        public Rect ActualMapBounds
        {
            get => (Rect)GetValue(ActualMapBoundsProperty);
            set => SetValue(ActualMapBoundsProperty, value);
        }

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

        

        //获取有效区 (代理 MapService)
        public Rect ValidRegion => _mapService.EffectiveRegion;

        // =========================
        // Internal State
        // =========================
        private readonly Dictionary<string, Image> _visibleTiles = new Dictionary<string, Image>();
        private readonly HashSet<string> _loadingTiles = new HashSet<string>();
        private readonly Dictionary<string, CancellationTokenSource> _loadingCts = new Dictionary<string, CancellationTokenSource>();

        private bool _isCleanedUp;
        private bool _isInternalUpdate = false;

        private int _mapVersion = 0;

        private VisualHost _staticHost;
        private VisualHost _editorHost;

        private ROIRegion _hoverROIRegion;

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
            SetCurrentValue(SelectedRegionsProperty, new ObservableCollection<ROIRegion>());
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
            Loaded += OnLoaded;

            _debounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer.Stop();
                UpdateTiles();
            };

            InitializeTools();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 如果之前被清理过，现在需要复活
            if (_isCleanedUp)
            {
                _isCleanedUp = false;

                // 1. 重建定时器 (Cleanup 中把它设为 null 了)
                if (_debounceTimer == null)
                {
                    _debounceTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(50)
                    };
                    _debounceTimer.Tick += (s, args) =>
                    {
                        _debounceTimer.Stop();
                        UpdateTiles();
                    };
                }

                // 2. 重新订阅 EventAggregator
                // 注意：EventAggregatorProperty 的回调可能不会再次触发，所以要手动订
                if (EventAggregator != null)
                {
                    EventAggregator.Subscribe(this);
                }

                // 3. 重新订阅 ItemsSource (ROI 数据监听)
                if (ItemsSource != null)
                {
                    foreach (var item in ItemsSource)
                    {
                        // 先退订一次保平安，再订阅
                        item.PropertyChanged -= OnItemPropertyChanged;
                        item.PropertyChanged += OnItemPropertyChanged;
                    }
                }


                // 5. 重建空间索引
                RebuildSpatialIndex();

                // 6. 强制刷新画面
                // 恢复加载状态
                if (!string.IsNullOrEmpty(_mapService.MapPath))
                {
                    // 重新触发一次加载逻辑（不会重置缩放，但会重新加载可见瓦片）
                    UpdateTiles();
                }
                RenderStaticLayer();
                RenderEditorLayer();
            }
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

        // 3. 集合内容变化回调 (负责重绘)
        // 注意：以前这里负责发 EventAggregator，现在删掉发事件代码，只保留重绘
        private void OnSelectedRegionsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // 如果集合变了（比如 Tool 往里 Add 了一个 ROI），View 负责刷新画面
            RenderEditorLayer();
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

            // 1. 清理旧列表
            if (e.OldValue is System.Collections.IEnumerable oldList)
            {
                if (oldList is INotifyCollectionChanged oldColl)
                    oldColl.CollectionChanged -= c.OnCollectionChanged;

                // 退订旧 Item 的事件
                foreach (object item in oldList)
                {
                    if (item is ROIRegion region)
                        region.PropertyChanged -= c.OnItemPropertyChanged;
                }
            }

            // 2. 绑定新列表
            if (e.NewValue is System.Collections.IEnumerable newList)
            {
                if (newList is INotifyCollectionChanged newColl)
                    newColl.CollectionChanged += c.OnCollectionChanged;

                // 必须遍历当前列表里“已经存在”的所有 Item，给它们一个个订阅上！
                // 之前你的代码漏了这一步，所以初始加载的 ROI 全都没反应。
                foreach (object item in newList)
                {
                    if (item is ROIRegion region)
                        region.PropertyChanged += c.OnItemPropertyChanged;
                }
            }

            // 3. 立即重绘
            c.RebuildSpatialIndex();
            c.RenderStaticLayer();
            c.RenderEditorLayer();
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            bool selectionChanged = false;

            // 1. 检查是否有“已选中”的物体被移除了
            if (e.Action == NotifyCollectionChangedAction.Remove ||
                e.Action == NotifyCollectionChangedAction.Replace ||
                e.Action == NotifyCollectionChangedAction.Reset)
            {
                if (e.OldItems != null)
                {
                    foreach (ROIRegion item in e.OldItems)
                    {
                        if (SelectedRegions.Contains(item))
                        {
                            // 从选中列表中剔除
                            item.IsSelected = false;
                            item.IsEditing = false;
                            SelectedRegions.Remove(item);
                            selectionChanged = true;
                        }
                        item.PropertyChanged -= OnItemPropertyChanged;
                    }
                }

                // 特别注意：Reset 时 e.OldItems 通常为 null，但意味着整个列表被清空或重置
                // 如果是 Reset，通常建议清空所有选中项
                if (e.Action == NotifyCollectionChangedAction.Reset)
                {
                    if (SelectedRegions.Count > 0)
                    {
                        foreach (var r in SelectedRegions) { r.IsSelected = false; r.IsEditing = false; }
                        SelectedRegions.Clear();
                        selectionChanged = true;
                    }
                }
            }

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                // ItemsSource 是当前最新的完整列表
                if (ItemsSource != null)
                {
                    foreach (var item in ItemsSource)
                    {
                        // 先退订一次保平安（防止重复订阅），再订阅
                        item.PropertyChanged -= OnItemPropertyChanged;
                        item.PropertyChanged += OnItemPropertyChanged;
                    }
                }
            }
            // =========================================================

            // 2. 处理常规新增 (Add)
            if (e.NewItems != null)
            {
                foreach (ROIRegion item in e.NewItems)
                    item.PropertyChanged += OnItemPropertyChanged;
            }

            // 2. 如果选中项确实变少了，需要检查“主选中项(SelectedROIRegion)”是否也挂了
            if (selectionChanged)
            {
                // 如果当前的主选中项已经不在 SelectedRegions 里了（说明刚被删了）
                if (SelectedROIRegion != null && !SelectedRegions.Contains(SelectedROIRegion))
                {
                    // 将主权移交给列表里剩下的最后一个，或者置空
                    var nextMain = SelectedRegions.LastOrDefault();

                    // 【必须加锁】更新属性，防止触发回调死循环
                    _isInternalUpdate = true;
                    try
                    {
                        SetCurrentValue(SelectedROIRegionProperty, nextMain);
                    }
                    finally
                    {
                        _isInternalUpdate = false;
                    }
                }
            }

            // 3. 常规重建索引和重绘
            RebuildSpatialIndex();
            RenderStaticLayer();
            RenderEditorLayer(); // 这次重绘时，幽灵已经不在 SelectedRegions 里了，所以会消失
        }

        private void OnItemPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 如果是点变了（比如 Setting 里的 Move/Inflate），需要重画 + 重建索引
            if (e.PropertyName == "Points")
            {
                RebuildSpatialIndex();
                RenderStaticLayer();
                RenderEditorLayer();
            }
            // 如果是样式变了 (线宽, 颜色)，只需要重画
            else if (e.PropertyName == "LineWidth" || e.PropertyName == "Color")
            {
                RenderStaticLayer();
                RenderEditorLayer();
            }
        }

        private static void OnSelectedROIRegionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var c = (RoiEditorCanvas)d;

            // 如果是内部逻辑正在更新，直接返回，别捣乱
            if (c._isInternalUpdate) return;

            var newItem = e.NewValue as ROIRegion;
            //调用内部逻辑，强制为单选模式(外部设置属性通常意味着单选)
            c.InternalSelect(newItem,isMultiSelect:false,updateProperty:false);
        }

        private static void OnEventAggregatorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var c = (RoiEditorCanvas)d;
            if (e.OldValue is IEventAggregator oldEa) oldEa.Unsubscribe(c);
            if (e.NewValue is IEventAggregator newEa) newEa.Subscribe(c);
        }

        private static void OnActualMapBoundsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var canvas = (RoiEditorCanvas)d;

            // 如果不是内部逻辑触发的变更，说明是外部有人在瞎改，直接改回去！
            if (!canvas._isSettingMapBoundsInternal)
            {
                // 恢复旧值
                canvas.SetCurrentValue(ActualMapBoundsProperty, e.OldValue);
                // 可选：打个 Debug 日志骂一句
                System.Diagnostics.Debug.WriteLine("[Warning] ActualMapBounds is read-only!");
            }
        }

        // =========================
        // Public API
        // =========================
        public void ReloadMap(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            // 不再只判断文件夹，而是“不存在文件夹 且 不存在文件”才退出
            if (!System.IO.Directory.Exists(path) && !System.IO.File.Exists(path))
                return;

            // 1. 加载地图数据 (MapService 会自动识别单图/切片)
            _mapService.LoadMap(path);
            // 加锁赋值
            _isSettingMapBoundsInternal = true;
            SetCurrentValue(ActualMapBoundsProperty, _mapService.EffectiveRegion);
            _isSettingMapBoundsInternal = false;
            MaxLevel = _mapService.MaxLevel;

            // 2. 版本号递增 (让旧的异步加载失效)
            Interlocked.Increment(ref _mapVersion);

            // 3. 清理旧缓存和加载任务
            _tileLoader.ClearCache();
            CancelAllLoading();

            // 4. 回收当前显示的瓦片
            foreach (var img in _visibleTiles.Values.ToList())
                _tilePool.Return(img);

            _visibleTiles.Clear();
            _loadingTiles.Clear();

            // 5. 重建空间索引 (世界尺寸变了，索引必须重置)
            RebuildSpatialIndex();

            // 6. 计算初始缩放
            // 单图模式下 MaxLevel=0，initialScale=1.0，这没问题
            // 后面的 FitToCoarsestAndCenter 会再次修正它
            double initialScale = 1.0 / Math.Pow(2, _mapService.MaxLevel);
            MainMatrix.Matrix = new Matrix(initialScale, 0, 0, initialScale, 0, 0);

            // 7. 适配视图 (Fit)
            if (IsLoaded && ActualWidth > 1 && ActualHeight > 1)
            {
                FitToCoarsestAndCenter();
            }
            else
            {
                RequestFitAfterLayout();
            }
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

            // === 单图模式分支 ===
            if (_mapService.IsSingleFileMode)
            {
                string key = "0_0_0"; // 假装它是唯一的瓦片
                if (_loadingTiles.Contains(key) || _visibleTiles.ContainsKey(key)) return;

                _mapService.GetWorldSize(out double w, out double h);
                var cts = new CancellationTokenSource();
                _loadingCts[key] = cts;
                _loadingTiles.Add(key);

                // 传入整图尺寸 w, h
                _ = LoadTileAsync(key, _mapService.MapPath, 0, 0, w, h, _mapVersion, 0, cts.Token);
                return; // [关键] 直接返回，不跑下面的瓦片逻辑
            }

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

                // 传入 tileWorld 作为宽高
                _ = LoadTileAsync(k, tilePath, x, y, tileWorld, tileWorld, _mapVersion, targetLevel, cts.Token);
            }

        }

        private async Task LoadTileAsync(string key, string path, double x, double y,
            double width,double height,
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
                    img.Width = width + overlap;
                    img.Height = height + overlap;
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
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                // 1. 绘制有效区域 (Valid Region)
                if (!_mapService.EffectiveRegion.IsEmpty)
                {
                    double zoom = Math.Max(1e-6, MainMatrix.Matrix.M11);
                    double strokeWidth = 1.0 / zoom;

                    var pen = new Pen(Brushes.Yellow, strokeWidth);

                    // 虚线标准间隔是 4 倍线宽 (2倍实线 + 2倍间隔)
                    // 计算如果画满一圈，大概有多少个 Dash
                    double perimeter = _mapService.EffectiveRegion.Width * 2 + _mapService.EffectiveRegion.Height * 2;
                    double estimatedDashCount = perimeter / (strokeWidth * 4);

                    // 阈值设为 5000 (经验值：超过这个数量 WPF 可能会渲染异常)
                    if (estimatedDashCount < 5000)
                    {
                        pen.DashStyle = DashStyles.Dash; // 数量少时，用虚线
                    }
                    else
                    {
                        pen.DashStyle = DashStyles.Solid; // 数量太多，降级为实线，防止渲染残留
                    }

                    if (pen.CanFreeze) pen.Freeze();

                    dc.DrawRectangle(null, pen, _mapService.EffectiveRegion);
                }

                // 2. 绘制静态 ROI
                if (ItemsSource != null)
                {
                    _renderer.DrawStaticLayer(dc, ItemsSource, _hoverROIRegion, MainMatrix.Matrix.M11);
                }
            }

            _staticHost.SetVisual(visual);
        }

        // [Controls/RoiEditorCanvas.xaml.cs]

        private void RenderEditorLayer()
        {
            // 1. 保护 _editorHost：防止在构造函数 InitializeRenderLayers() 之前被调用
            if (_editorHost == null) return;

            var visual = new DrawingVisual();

            // 2. 保护 SelectedRegions：防止为 null 时访问 .Count 导致崩溃
            // 修改处：增加 SelectedRegions != null 判断
            if (SelectedRegions != null && SelectedRegions.Count > 0)
            {
                using (var dc = visual.RenderOpen())
                {
                    // _renderer 是字段初始化，通常不为空，但保险起见也可以检查
                    _renderer?.DrawEditorLayer(dc, SelectedRegions, MainMatrix.Matrix);
                }
            }

            _editorHost.SetVisual(visual);
        }

        // =========================
        // 模式切换回调
        // =========================
        private static void OnROIOperationModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RoiEditorCanvas canvas)
            {
                var newMode = (ROIOperationMode)e.NewValue;
                canvas.SwitchTool(newMode);

                // 如果切换回 Pan 模式，清空选中状态
                // 也可以扩展逻辑：只要切出 Select/Create 模式就清空
                if (newMode == ROIOperationMode.ROI_OS_Pan)
                {
                    canvas.ClearSelection();
                }
            }
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

        /// <summary>
        /// 核心选择方法：工具层和外部都调用这个
        /// </summary>
        /// <param name="item"></param>
        /// <param name="isMultiSelect"></param>
        internal void SelectROIRegion(ROIRegion item,bool isMultiSelect = false)
        {        
            //调用内部实现，允许更新属性
            InternalSelect(item,isMultiSelect,updateProperty:true);
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

        /// <summary>
        /// 命中测试
        /// </summary>
        /// <param name="wPos">世界坐标</param>
        /// <param name="filter">可选过滤器，用于排除不想命中的物体（如被 ActiveROI 过滤）</param>
        internal ROIRegion HitTestROIRegion(Point wPos, Predicate<ROIRegion> filter = null)
        {
            if (_spatialIndex == null)
                return HitTestROIRegionLegacy(wPos, filter); // 传给 Legacy

            _hitTestCache.Clear();
            _spatialIndex.Query(wPos, _hitTestCache);
            if (_hitTestCache.Count == 0) return null;

            ROIRegion bestHit = null;
            int bestIndex = -1;

            foreach (var item in _hitTestCache)
            {
                // 【过滤器检查】
                if (filter != null && !filter(item)) continue;

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

        private ROIRegion HitTestROIRegionLegacy(Point wPos, Predicate<ROIRegion> filter)
        {
            if (ItemsSource == null) return null;

            for (int i = ItemsSource.Count - 1; i >= 0; i--)
            {
                var roi = ItemsSource[i];
                if (roi?.Points == null || roi.Points.Count < 3) continue;

                // 【过滤器检查】
                if (filter != null && !filter(roi)) continue;

                var geom = RoiRenderer.BuildGeometry(roi);
                if (!geom.Bounds.Contains(wPos)) continue;

                if (geom.FillContains(wPos)) return roi;

                var pen = new Pen(Brushes.Transparent, 6.0 / Math.Max(1e-6, MainMatrix.Matrix.M11));
                if (geom.StrokeContains(pen, wPos)) return roi;
            }

            return null;
        }

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

        /// <summary>
        /// 选择Region（单选或者多选）
        /// </summary>
        /// <param name="item"></param>
        /// <param name="isMultiSelect"></param>
        /// <param name="updateProperty"></param>
        private void InternalSelect(ROIRegion item,bool isMultiSelect,bool updateProperty)
        {
            //如果是单选模式，先清空现有的，并且先跳过重绘
            if(!isMultiSelect)
            {
                ClearSelection(skipRedraw:true);
            }

            if(item != null)
            {
                //如果还没有选中，加进去
                if(!SelectedRegions.Contains(item))
                {
                    item.IsSelected = true;
                    SelectedRegions.Add(item);
                }

                //如果更新属性标志为真，且属性当前值不对，则更新属性
                if(updateProperty && SelectedROIRegion != item)
                {
                    using (PreventRecursiveSelection())
                    {
                        SetCurrentValue(SelectedROIRegionProperty, item);
                    }
                }
            }
            else
            {
                //如果item为null且是单选模式，上面ClearSelection已经清空了
                if(updateProperty && SelectedROIRegion != null)
                {
                    using (PreventRecursiveSelection())
                    {
                        SetCurrentValue(SelectedROIRegionProperty, null);
                    }
                }
            }

            RenderEditorLayer();
            RenderStaticLayer();
        }

        /// <summary>
        /// 反选：用于Shift取消某一个
        /// </summary>
        /// <param name="item"></param>
        public void DeselectROIRegion(ROIRegion item)
        {
            if(item != null && SelectedRegions.Contains(item))
            {
                item.IsSelected = false;
                item.IsEditing = false;
                SelectedRegions.Remove(item);
                //如果取消的正好是主选中项，移交列表最后一个(或者置空)
                if(SelectedROIRegion == item)
                {
                    var nextMain = SelectedRegions.LastOrDefault();
                    // 移交主权时加锁！
                    using (PreventRecursiveSelection())
                    {
                        SetCurrentValue(SelectedROIRegionProperty, nextMain);
                    }
                }

                RenderEditorLayer();
                RenderStaticLayer();
            }
        }

        /// <summary>
        /// 点坐标钳制：保证点在有效区内
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public Point ClampToValidRegion(Point p)
        {
            if (ValidRegion.IsEmpty) return p;

            double x = Math.Max(ValidRegion.Left, Math.Min(ValidRegion.Right, p.X));
            double y = Math.Max(ValidRegion.Top, Math.Min(ValidRegion.Bottom, p.Y));
            return new Point(x, y);
        }

        // 矩形钳制：用于拖拽整个 ROI 时，保证不拖出去
        public Rect ClampRectToValidRegion(Rect r)
        {
            if (ValidRegion.IsEmpty) return r;

            double x = r.X;
            double y = r.Y;

            // 简单的平移限制：如果左边出去了就贴左边，右边出去了就贴右边
            if (x < ValidRegion.Left) x = ValidRegion.Left;
            if (x + r.Width > ValidRegion.Right) x = ValidRegion.Right - r.Width;

            if (y < ValidRegion.Top) y = ValidRegion.Top;
            if (y + r.Height > ValidRegion.Bottom) y = ValidRegion.Bottom - r.Height;

            return new Rect(x, y, r.Width, r.Height);
        }

        public void ClearSelection(bool skipRedraw = false)
        {
            if (SelectedRegions.Count == 0) return;

            foreach(var r in SelectedRegions)
            {
                r.IsSelected = false;
                r.IsEditing = false;
            }
            SelectedRegions.Clear();

            if(SelectedROIRegion != null)
            {
                // 清空属性时加锁
                using (PreventRecursiveSelection())
                {
                    SetCurrentValue(SelectedROIRegionProperty, null);
                }
            }

            if(!skipRedraw)
            {
                RenderEditorLayer();
                RenderStaticLayer();
            }
        }

        // 工具初始化逻辑
        private void InitializeTools()
        {
            _tools = new Dictionary<ROIOperationMode, IInteractionTool>();
            var assembly = Assembly.GetExecutingAssembly();
            var toolTypes = assembly.GetTypes()
                .Where(t => typeof(IInteractionTool).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .Where(t => t.GetCustomAttribute<RoiToolAttribute>() != null);

            foreach (var type in toolTypes)
            {
                var attribute = type.GetCustomAttribute<RoiToolAttribute>();
                var toolInstance = (IInteractionTool)Activator.CreateInstance(type, this);
                if (!_tools.ContainsKey(attribute.Mode)) _tools.Add(attribute.Mode, toolInstance);
            }
            _currentTool = _tools.ContainsKey(ROIOperationMode.ROI_OS_Pan) ? _tools[ROIOperationMode.ROI_OS_Pan] : null;
            _currentTool?.Activate();
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

        private IDisposable PreventRecursiveSelection()
        {
            return new ScopeGuard(() => _isInternalUpdate = true, () => _isInternalUpdate = false);
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
            _spatialIndex = null;
            _hitTestCache.Clear();

            // 8) EventAggregator 退订，防止控件被外部引用住
            try { EventAggregator?.Unsubscribe(this); } catch { }

            // 9) 退订所有 Item 事件
            if (ItemsSource != null)
            {
                foreach (var item in ItemsSource)
                    item.PropertyChanged -= OnItemPropertyChanged;
            }
        }

    }
}
