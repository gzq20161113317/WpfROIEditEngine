using Caliburn.Micro;
using RoiEditor.Enums;
using RoiEditor.Events;
using RoiEditor.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace RoiEditor.ViewModels.Component
{
    public class CanvasViewModel : Screen,
        IHandle<ROIOperationModeChangedEvent>,
        IHandle<ActiveROIChangedEvent>
    {
        #region Fields & Dependencies
        private readonly IEventAggregator _eventAggregator;

        // 状态字段
        private ROIOperationMode _currentOperationMode = ROIOperationMode.ROI_OS_Pan;
        private string _mapPath;
        private int _currentLevel;
        private int _maxLevel;
        private Rect _actualMapBounds;

        // 选中对象引用
        private ROI _activeROI;
        private ROIRegion _selectedROIRegion;

        // 统计字符串
        private string _totalAreaInfo;
        private string _activeROIAreaInfo;
        private string _selectedAreaInfo;

        // 全局 ROI 列表 (来自 RoiMainViewModel)
        private BindableCollection<ROIRegion> _flatROIRegions;
        #endregion

        #region Properties

        public IEventAggregator EventAggregator => _eventAggregator;
        public string LevelStatusString => $"Layer: {CurrentLevel} / {MaxLevel}";

        // 【核心】当前选中的 ROI 列表 (Source of Truth)
        // 这个集合会双向绑定到 View 的 SelectedRegions
        public ObservableCollection<ROIRegion> MySelection { get; } = new ObservableCollection<ROIRegion>();

        public string MapPath
        {
            get => _mapPath;
            set { _mapPath = value; NotifyOfPropertyChange(() => MapPath); }
        }

        public ROIOperationMode CurrentOperationMode
        {
            get => _currentOperationMode;
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
                NotifyOfPropertyChange(() => LevelStatusString);
            }
        }

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
                UpdateAllStatistics(); // ActiveROI 变了，重新统计
            }
        }

        // 当前选中的主 ROI (单选或最后选中的那个)
        public ROIRegion SelectedROIRegion
        {
            get => _selectedROIRegion;
            set
            {
                if (ReferenceEquals(_selectedROIRegion, value)) return;
                _selectedROIRegion = value;
                NotifyOfPropertyChange(() => SelectedROIRegion);

                // 联动逻辑：选中 Region -> 激活其 Parent
                if (_selectedROIRegion != null && _selectedROIRegion.Parent != null)
                {
                    ActiveROI = _selectedROIRegion.Parent;
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

                // 1. 旧列表解绑
                if (_flatROIRegions != null)
                {
                    _flatROIRegions.CollectionChanged -= OnFlatRegionsChanged;
                    foreach (var item in _flatROIRegions) item.PropertyChanged -= OnRegionPropertyChanged;
                }

                _flatROIRegions = value;

                // 2. 新列表绑定
                if (_flatROIRegions != null)
                {
                    _flatROIRegions.CollectionChanged += OnFlatRegionsChanged;
                    foreach (var item in _flatROIRegions) item.PropertyChanged += OnRegionPropertyChanged;
                }

                NotifyOfPropertyChange(() => FlatROIRegions);
                UpdateAllStatistics();
            }
        }

        public Rect ActualMapBounds
        {
            get => _actualMapBounds;
            set
            {
                if (_actualMapBounds == value) return;
                _actualMapBounds = value;
                NotifyOfPropertyChange(() => ActualMapBounds);
                // 广播地图边界变化
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

        #endregion

        #region Constructor & Lifecycle

        public CanvasViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _flatROIRegions = new BindableCollection<ROIRegion>();
            _flatROIRegions.CollectionChanged += OnFlatRegionsChanged;

            // 监听选中列表变化
            MySelection.CollectionChanged += OnSelectionChanged;
        }

        protected override void OnActivate()
        {
            base.OnActivate();
            _eventAggregator.Subscribe(this);
        }

        protected override void OnDeactivate(bool close)
        {
            // 解绑 FlatROIRegions
            if (FlatROIRegions != null)
            {
                FlatROIRegions.CollectionChanged -= OnFlatRegionsChanged;
            }

            // 解绑 MySelection
            if (close)
            {
                foreach (var r in MySelection)
                    r.PropertyChanged -= OnRegionPropertyChanged;

                MySelection.CollectionChanged -= OnSelectionChanged;
                MySelection.Clear();
            }

            base.OnDeactivate(close);
            _eventAggregator.Unsubscribe(this);
        }

        #endregion

        #region Event Handlers (External)

        public void Handle(ActiveROIChangedEvent message)
        {
            ActiveROI = message.ActiveROI;
        }

        public void Handle(ROIOperationModeChangedEvent message)
        {
            CurrentOperationMode = message.CurrentOperationMode;
        }

        #endregion

        #region Collection & Property Change Handlers

        // 1. 处理选中列表变化 (MySelection)
        private void OnSelectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // A. 新选中的：监听 Resize/Inflate 导致的 Points 变化
            if (e.NewItems != null)
            {
                foreach (ROIRegion item in e.NewItems)
                {
                    item.IsSelected = true;
                    item.PropertyChanged += OnRegionPropertyChanged;
                }
            }

            // B. 移除的：取消监听
            if (e.OldItems != null)
            {
                foreach (ROIRegion item in e.OldItems)
                {
                    item.IsSelected = false;
                    item.PropertyChanged -= OnRegionPropertyChanged;
                }
            }

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                // Reset 发生时简单归零，或者遍历旧列表解绑(如果已缓存)
                SelectedAreaInfo = "Selected: 0 px²";
            }

            // C. 无论何种变动，都更新统计
            UpdateAllStatistics();
        }

        // 2. 处理全局列表变化 (FlatROIRegions)
        private void OnFlatRegionsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (ROIRegion item in e.NewItems)
                    item.PropertyChanged += OnRegionPropertyChanged;
            }

            if (e.OldItems != null)
            {
                foreach (ROIRegion item in e.OldItems)
                    item.PropertyChanged -= OnRegionPropertyChanged;
            }

            UpdateAllStatistics();
        }

        // 3. 处理单个 ROI 属性变化 (Points 变化 -> 面积重算)
        private void OnRegionPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Points")
            {
                UpdateAllStatistics(); // 实时更新
            }
        }

        #endregion

        #region Statistics & Helpers

        private void UpdateAllStatistics()
        {
            // 1. 计算当前选中的 (直接使用 MySelection)
            double selArea = 0;
            if (MySelection.Count > 0)
            {
                foreach (var r in MySelection) selArea += CalculateArea(r);
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

        private string FormatArea(double areaPx)
        {
            return $"{areaPx:F0} px²";
        }

        #endregion
    }
}