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
        private ObservableCollection<ROI> _roiCollection;

        #endregion

        #region Properties

        public IEventAggregator EventAggregator => _eventAggregator;
        public string LevelStatusString => $"Layer: {CurrentLevel} / {MaxLevel}";

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

        // 1. 数据源（仓库）：树状结构，包含所有 ROI
        public ObservableCollection<ROI> ROICollection
        {
            get => _roiCollection;
            set
            {
                if (_roiCollection == value) return;
                
                //一般不会变更ROI列表
                //A.旧列表彻底解绑
                if(_roiCollection != null)
                {
                    _roiCollection.CollectionChanged -= OnROICollectionChanged;//解绑旧ROI集合的CollectionChanged事件(ROI的增删的时候会触发)
                    foreach (var roi in _roiCollection)
                    {
                        roi.Regions.CollectionChanged -= OnSubRegionsChanged;//解绑旧ROI集合的每一个ROI的Regions的CollectionChanged事件
                        foreach (var r in roi.Regions)
                        {
                            r.PropertyChanged -= OnRegionPropertyChanged;//解绑旧ROI集合的每一个ROI的Regions的每一个Region的PropertyChanged事件
                        }
                    }
                }

                _roiCollection = value;

                //B.新列表绑定
                if(_roiCollection != null)
                {
                    _roiCollection.CollectionChanged += OnROICollectionChanged;//绑定新ROI集合的CollectionChanged事件(ROI的增删的时候会触发)
                    foreach (var roi in _roiCollection)
                    {
                        roi.Regions.CollectionChanged += OnSubRegionsChanged;//绑定新ROI集合的每一个ROI的Regions的CollectionChanged事件
                        foreach(var r in roi.Regions)
                        {
                            r.PropertyChanged += OnRegionPropertyChanged;//绑定新ROI集合的每一个ROI的Regions的每一个Region的PropertyChanged事件
                        }
                    }
                }

                // 通知 View
                NotifyOfPropertyChange(() => ROICollection);
                UpdateAllStatistics();
            }
        }

        // 2. 选中态（购物车）：扁平列表，仅包含被选中的 ROIRegion 引用
        // 这个集合会双向绑定到 View 的 SelectedRegions
        public ObservableCollection<ROIRegion> MySelection { get; } = new ObservableCollection<ROIRegion>();

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
            // 解绑 MySelection
            if (close)
            {
                if (ROICollection != null)
                {
                    ROICollection.CollectionChanged -= OnROICollectionChanged;
                    // 如果需要彻底解绑，也可以遍历解绑 SubRegions
                }

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
        /// <summary>
        /// 第一层监听：ROI组的增删
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnROICollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            //新增ROI时
            if(e.NewItems != null)
            {
                foreach(ROI roi in e.NewItems)
                {
                    //监听新增的ROI内部的Regions的变化
                    roi.Regions.CollectionChanged += OnSubRegionsChanged;
                    //监听新增的ROI内部的Region的属性的变化(Points)
                    foreach(var r in roi.Regions)
                    {
                        r.PropertyChanged += OnRegionPropertyChanged;
                    }
                }
            }

            //删除ROI时
            if(e.OldItems != null)
            {
                foreach(ROI roi in e.OldItems)
                {
                    roi.Regions.CollectionChanged -= OnSubRegionsChanged;
                    foreach (var r in roi.Regions) r.PropertyChanged -= OnRegionPropertyChanged;
                }
            }

            //ROI组的增删也需要重新计算
            UpdateAllStatistics();
        }

        /// <summary>
        /// 第二层监听：Region的增删
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnSubRegionsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            //新增Region时
            if(e.NewItems != null)
            {
                //监听Region内部属性的变化
                foreach(ROIRegion r in e.NewItems)
                {
                    r.PropertyChanged += OnRegionPropertyChanged;
                }
            }

            //删除Region时
            if(e.OldItems != null)
            {
                //解绑对Region内部属性的变化监听
                foreach (ROIRegion r in e.OldItems) 
                    r.PropertyChanged -= OnRegionPropertyChanged;
            }

            UpdateAllStatistics();
        }

        /// <summary>
        /// 第三层监听：处理单个 ROI 属性变化 (Points 变化 -> 面积重算)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnRegionPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Points")
            {
                UpdateAllStatistics(); // 实时更新
            }
        }

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
            if(ROICollection != null)
            {
                foreach(var roi in ROICollection)
                {
                    if(roi.Regions != null)
                    {
                        foreach(var r in roi.Regions)
                        {
                            totalArea += CalculateArea(r);
                        }
                    }
                }
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