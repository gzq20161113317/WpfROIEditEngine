using Caliburn.Micro;
using RoiEditor.Enums;
using RoiEditor.Events;
using RoiEditor.Models;
using RoiEditor.ViewModels.Component;
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
            Canvas = new CanvasViewModel(eventAggregator);

            Canvas.FlatROIRegions = FlatRegions;

            Items.Add(ToolBar);
            Items.Add(ROIListVM);
            Items.Add(Canvas);

            //核心逻辑：数据同步
            //当用户再ROIListVM里增加/删除"墙"时，MainVM负责把"砖"搬运到FlatRegions
            ROIListVM.ROIS.CollectionChanged += OnROIListChanged;
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
                    newROI.Regions.CollectionChanged += (s, args) => SyncRegions(args);
                }
            }
            if(e.OldItems != null)
            {
                foreach(ROI oldRoi in e.OldItems)
                {
                    FlatRegions.RemoveRange(oldRoi.Regions);
                }
            }
        }

        /// <summary>
        /// ROI的Regions列表的2号委托
        /// 用于将新加的Region拍扁喂给Canvas，渲染图像
        /// </summary>
        /// <param name="e"></param>
        private void SyncRegions(NotifyCollectionChangedEventArgs e)
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
            base.OnActivate();
            _eventAggregator.Subscribe(this);
        }

        protected override void OnDeactivate(bool close)
        {
            base.OnDeactivate(close);
            _eventAggregator.Unsubscribe(this);
        }

    }
}
