using Caliburn.Micro;
using RoiEditor.Enums;
using RoiEditor.Events;
using RoiEditor.Models;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace RoiEditor.ViewModels.Component
{
    public class CanvasViewModel:Screen, 
        IHandle<ROIOperationModeChangedEvent>,
        IHandle<ActiveROIChangedEvent>,
        IHandle<RegionSelectionChangedEvent>
    {
        private readonly IEventAggregator _eventAggregator;
        private ROIOperationMode _currentOperationMode = ROIOperationMode.ROI_OS_Pan;
        private string _mapPath;
        private int _currentLevel;
        private int _maxLevel;
        private ROI _activeROI;
        private ROIRegion _selectedROIRegion;
        private Rect _actualMapBounds;
        private string _totalAreaInfo;//所有ROI
        private string _activeROIAreaInfo;//当前ROI
        private string _selectedAreaInfo;//当前选中(支持多选)
        // 本地缓存的多选列表
        private List<ROIRegion> _multiSelectedRegions = new List<ROIRegion>();
        private BindableCollection<ROIRegion> _flatROIRegions;
    
        public IEventAggregator EventAggregator => _eventAggregator;
        // 用于界面显示的格式化字符串
        public string LevelStatusString => $"Layer: {CurrentLevel} / {MaxLevel}";

        public CanvasViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _flatROIRegions = new BindableCollection<ROIRegion>();
        }

        public string MapPath
        {
            get => _mapPath;
            set { _mapPath = value; NotifyOfPropertyChange(() => MapPath); }
        }

        public ROIOperationMode CurrentOperationMode
        {
            get { return _currentOperationMode; }
            set
            {
                if (_currentOperationMode == value) return;
                _currentOperationMode = value;
                NotifyOfPropertyChange(() => CurrentOperationMode);
            }
        }

        public int CurrentLevel
        {
            get => _currentLevel;
            set
            {
                if (_currentLevel == value) return;
                _currentLevel = value;
                NotifyOfPropertyChange(() => CurrentLevel);
                NotifyOfPropertyChange(() => LevelStatusString); // 触发状态字符串更新
            }
        }


        // 地图加载完成后设置这个值
        public int MaxLevel
        {
            get => _maxLevel;
            set
            {
                _maxLevel = value;
                NotifyOfPropertyChange(() => MaxLevel);
                NotifyOfPropertyChange(() => LevelStatusString);
            }
        }

        public ROI ActiveROI
        {
            get => _activeROI;
            set 
            { 
                _activeROI = value;
                NotifyOfPropertyChange(() => ActiveROI);
                UpdateAllStatistics();
            }
        }

        // 当前选中 ROI
        public ROIRegion SelectedROIRegion
        {
            get => _selectedROIRegion;
            set
            {
                if (ReferenceEquals(_selectedROIRegion, value)) return;
                _selectedROIRegion = value;
                NotifyOfPropertyChange(() => SelectedROIRegion);
                // 当用户在画布上点击 Region 时，必须告诉全系统：现在的 ActiveROI 是这个 Region 的爹！
                if (_selectedROIRegion != null && _selectedROIRegion.Parent != null)
                {
                    // 1. 更新自身的 ActiveROI
                    ActiveROI = _selectedROIRegion.Parent;

                    // 2. 发送事件，确保 SelectSettingViewModel 能收到并更新 _currentActiveROI
                    _eventAggregator.PublishOnUIThread(new ActiveROIChangedEvent(_selectedROIRegion.Parent));
                }
            }
        }

        public BindableCollection<ROIRegion> FlatROIRegions
        {
            get => _flatROIRegions;
            set
            {
                if (ReferenceEquals(_flatROIRegions, value)) return;

                // 1. 搬家前：在旧房子(List A)里拆掉监控摄像头
                if (_flatROIRegions != null)
                {
                    _flatROIRegions.CollectionChanged -= OnFlatRegionsChanged;
                    foreach (var item in _flatROIRegions) item.PropertyChanged -= OnRegionPropertyChanged;
                }

                _flatROIRegions = value;

                // 2. 搬家后：在新房子(List B)里装上监控摄像头
                if (_flatROIRegions != null)
                {
                    _flatROIRegions.CollectionChanged += OnFlatRegionsChanged;
                    foreach (var item in _flatROIRegions) item.PropertyChanged += OnRegionPropertyChanged;
                }

                // 3. 【关键】大喊一声：“地址换了！”，让 View 也赶紧看新房子
                NotifyOfPropertyChange(() => FlatROIRegions);

                UpdateAllStatistics();
            }
        }

        public Rect ActualMapBounds
        {
            get => _actualMapBounds;
            set
            {
                if(_actualMapBounds == value) return;
                _actualMapBounds = value;
                NotifyOfPropertyChange(() => ActualMapBounds);
                // 核心逻辑：只要地图边界变了，就通知全系统
                _eventAggregator.PublishOnUIThread(new MapInfoChangedEvent(_actualMapBounds));
            }
        }

        public string TotalAreaInfo
        {
            get => _totalAreaInfo;
            set { _totalAreaInfo = value; NotifyOfPropertyChange(() => TotalAreaInfo); }
        }

        public string ActiveROIAreaInfo
        {
            get => _activeROIAreaInfo;
            set { _activeROIAreaInfo = value; NotifyOfPropertyChange(() => ActiveROIAreaInfo); }
        }

        public string SelectedAreaInfo
        {
            get => _selectedAreaInfo;
            set { _selectedAreaInfo = value; NotifyOfPropertyChange(() => SelectedAreaInfo); }
        }

        public void Handle(ActiveROIChangedEvent message)
        {
            ActiveROI = message.ActiveROI;
        }

        public void Handle(ROIOperationModeChangedEvent message)
        {
            CurrentOperationMode = message.CurrentOperationMode;
        }
        // 1. 处理多选变化
        public void Handle(RegionSelectionChangedEvent message)
        {
            _multiSelectedRegions = message.SelectedRegions;
            UpdateAllStatistics();
        }

        // 2. 监听属性变化 (Points 变了，面积就要变)
        private void OnRegionPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Points")
            {
                UpdateAllStatistics();
            }
        }
        private void OnFlatRegionsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // 1. 处理新增项
            if (e.NewItems != null)
            {
                foreach (ROIRegion item in e.NewItems)
                {
                    // 这里的 += 保证了以后无论你在哪里改这个 ROI 的 Points，Canvas 都会知道
                    item.PropertyChanged += OnRegionPropertyChanged;
                }
            }

            // 2. 处理移除项
            if (e.OldItems != null)
            {
                foreach (ROIRegion item in e.OldItems)
                {
                    item.PropertyChanged -= OnRegionPropertyChanged;
                }
            }

            // 3. 处理清空 (Reset)
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                // 如果发生了 Reset，最稳妥的是全部重算，但解订阅比较麻烦
                // 通常建议尽量避免 Reset，或者在这里尝试遍历旧列表解订阅（如果能拿到的话）
            }

            UpdateAllStatistics();
        }


        // ==========================================
        // 【核心】统计算法
        // ==========================================
        private void UpdateAllStatistics()
        {
            // 1. 计算当前选中的 (多选)
            double selArea = 0;
            if (_multiSelectedRegions.Count > 0)
            {
                foreach (var r in _multiSelectedRegions) selArea += CalculateArea(r);
            }
            SelectedAreaInfo = $"Selected Regions: {FormatArea(selArea)}";

            // 2. 计算当前 ActiveROI 的
            double roiArea = 0;
            if (ActiveROI != null && ActiveROI.Regions != null)
            {
                foreach (var r in ActiveROI.Regions) roiArea += CalculateArea(r);
            }
            ActiveROIAreaInfo = $"Active ROI: {FormatArea(roiArea)}";

            // 3. 计算全局所有的
            double totalArea = 0;
            if (FlatROIRegions != null)
            {
                foreach (var r in FlatROIRegions) totalArea += CalculateArea(r);
            }
            TotalAreaInfo = $"Total ROI: {FormatArea(totalArea)}";
        }

        // 鞋带公式计算面积
        private double CalculateArea(ROIRegion region)
        {
            if (region == null || region.Points == null || region.Points.Count < 3) return 0;

            double area = 0;
            var points = region.Points;
            int count = points.Count;
            for (int i = 0; i < count; i++)
            {
                var p1 = points[i];
                var p2 = points[(i + 1) % count];
                area += (p1.X * p2.Y - p2.X * p1.Y);
            }
            return System.Math.Abs(area) / 2.0;
        }

        // 格式化帮助方法 (支持 pixel -> um 扩展)
        private string FormatArea(double areaPx)
        {
            // 这里可以乘以后续的 um 系数
            // double pixelRatio = 0.5; // 0.5um/pixel
            // double areaUm = areaPx * pixelRatio * pixelRatio;
            return $"{areaPx:F0} px²";
        }


        protected override void OnActivate()
        {
            // 监听总列表增删
            FlatROIRegions.CollectionChanged += OnFlatRegionsChanged;
            base.OnActivate();
            _eventAggregator.Subscribe(this);
        }

        protected override void OnDeactivate(bool close)
        {
            if (FlatROIRegions != null)
            {
                FlatROIRegions.CollectionChanged -= OnFlatRegionsChanged;
            }
            if(close)
            {
                foreach (var r in _multiSelectedRegions)
                    r.PropertyChanged -= OnRegionPropertyChanged;
                _multiSelectedRegions.Clear();
                _multiSelectedRegions = null;
            }   
            base.OnDeactivate(close);
            _eventAggregator.Unsubscribe(this);
        }
    }
}
