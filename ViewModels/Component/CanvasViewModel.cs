using Caliburn.Micro;
using RoiEditor.Enums;
using RoiEditor.Events;
using RoiEditor.Models;
using System;
using System.Collections.Generic;
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

        /// <summary>
        /// 永远不要给FlatROIRegions新引用，只Add，Remove，AddRange，Clear
        /// </summary>
        public BindableCollection<ROIRegion> FlatROIRegions { get; set; } = new BindableCollection<ROIRegion>();
        public IEventAggregator EventAggregator => _eventAggregator;
        // 用于界面显示的格式化字符串
        public string LevelStatusString => $"Layer: {CurrentLevel} / {MaxLevel}";

        public CanvasViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            // 监听总列表增删
            FlatROIRegions.CollectionChanged += (s, e) => UpdateAllStatistics();
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
            set { _activeROI = value; NotifyOfPropertyChange(() => ActiveROI); }
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
            // A. 退订旧的 (防止内存泄漏)
            foreach (var r in _multiSelectedRegions)
                r.PropertyChanged -= OnRegionPropertyChanged;

            // B. 更新列表
            _multiSelectedRegions = message.SelectedRegions;

            // C. 订阅新的 (监听拖拽变化)
            foreach (var r in _multiSelectedRegions)
                r.PropertyChanged += OnRegionPropertyChanged;

            // D. 立即计算
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
            SelectedAreaInfo = $"Selected: {FormatArea(selArea)}";

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
            TotalAreaInfo = $"Total: {FormatArea(totalArea)}";
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
            base.OnActivate();
            _eventAggregator.Subscribe(this);
        }

        protected override void OnDeactivate(bool close)
        {
            base.OnDeactivate(close);
            _eventAggregator.Unsubscribe(this);
            foreach (var r in _multiSelectedRegions)
                r.PropertyChanged -= OnRegionPropertyChanged;
            _multiSelectedRegions.Clear();
            _multiSelectedRegions = null;
        }


    }
}
