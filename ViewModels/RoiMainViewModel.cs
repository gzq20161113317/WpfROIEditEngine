using Caliburn.Micro;
using RoiEditor.Enums;
using RoiEditor.Events;
using RoiEditor.Models;
using RoiEditor.ViewModels.Component;
using RoiEditor.ViewModels.Component.Setting;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace RoiEditor.ViewModels
{
    public class RoiMainViewModel : Conductor<IScreen>.Collection.AllActive
    {

        private readonly IEventAggregator _eventAggregator;
        private readonly IWindowManager _windowManager;

        //公开子模块给View使用
        public ToolBarViewModel ToolBar { get; set; }
        public CanvasViewModel Canvas { get; set; }
        public ROIListViewModel ROIListVM { get; set; }
        public SettingViewModel SettingVM { get; set; }

        //扁平化数据
        public BindableCollection<ROIRegion> FlatRegions { get; } = new BindableCollection<ROIRegion>();

        public IEventAggregator EventAggregator => _eventAggregator;

        public RoiMainViewModel(IEventAggregator eventAggregator,IWindowManager windowManager)
        {
            _eventAggregator = eventAggregator;
            _windowManager = windowManager;

            //初始化所有子组件
            ToolBar = new ToolBarViewModel(eventAggregator);
            ROIListVM = new ROIListViewModel(eventAggregator,windowManager);
            SettingVM = new SettingViewModel(eventAggregator);
            Canvas = new CanvasViewModel(eventAggregator);

            Canvas.FlatROIRegions = FlatRegions;

            Items.Add(ToolBar);
            Items.Add(ROIListVM);
            Items.Add(SettingVM);
            Items.Add(Canvas);
        }


        #region Action
        /// <summary>
        /// ROIS列表的集合变更事件
        /// 当ROIS列表发生，增 删的时候触发该事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnROIListChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if(e.NewItems != null)
            {
                foreach(ROI newROI in e.NewItems)
                {
                    FlatRegions.AddRange(newROI.Regions);
                    // 监听这面墙后续的砖块增减
                    newROI.Regions.CollectionChanged += OnRegionsCollectionChanged;
                }
            }
            if(e.OldItems != null)
            {
                foreach(ROI oldRoi in e.OldItems)
                {
                    FlatRegions.RemoveRange(oldRoi.Regions);
                    oldRoi.Regions.CollectionChanged -= OnRegionsCollectionChanged;
                }
            }
        }

        /// <summary>
        /// ROI的Regions列表集合变更事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnRegionsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null) FlatRegions.AddRange(e.NewItems.Cast<ROIRegion>());
            if (e.OldItems != null) FlatRegions.RemoveRange(e.OldItems.Cast<ROIRegion>());
        }

        #endregion

        public void LoadMap(string path)
        {
            Canvas.MapPath = path;
        }

        protected override void OnActivate()
        {
            //核心逻辑：数据同步
            //当用户再ROIListVM里增加/删除"墙"时，MainVM负责把"砖"搬运到FlatRegions
            ROIListVM.ROIS.CollectionChanged += OnROIListChanged;

            if (ROIListVM.ROIS.Count > 0)
            {
                foreach (var roi in ROIListVM.ROIS)
                {
                    // 同步现有数据
                    FlatRegions.AddRange(roi.Regions);
                    // 挂载监听器
                    roi.Regions.CollectionChanged += OnRegionsCollectionChanged;
                }
            }
            base.OnActivate();
            _eventAggregator.Subscribe(this);
        }

        protected override void OnDeactivate(bool close)
        {
            // A. 退订 ROI 列表的主监听
            if (ROIListVM != null && ROIListVM.ROIS != null)
            {
                ROIListVM.ROIS.CollectionChanged -= OnROIListChanged;

                // B. 遍历现有 ROI，退订每一个 Region 的监听
                foreach (var roi in ROIListVM.ROIS)
                {
                    // 使用我们之前提取的具名方法进行解订阅
                    roi.Regions.CollectionChanged -= OnRegionsCollectionChanged;
                }
            }
            base.OnDeactivate(close);
            _eventAggregator.Unsubscribe(this);
        }

    }
}
