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

            Canvas.ROICollection = ROIListVM.ROIS;

            Items.Add(ToolBar);
            Items.Add(ROIListVM);
            Items.Add(SettingVM);
            Items.Add(Canvas);
        }




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
