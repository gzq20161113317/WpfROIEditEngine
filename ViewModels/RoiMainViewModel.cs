using Caliburn.Micro;
using RoiEditor.Enums;
using RoiEditor.Events;
using RoiEditor.Models;
using RoiEditor.ViewModels.Component;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace RoiEditor.ViewModels
{
    public class RoiMainViewModel : Conductor<IScreen>.Collection.AllActive
    {
        //公开子模块给View使用
        public ToolBarViewModel ToolBar { get; set; }
        public CanvasViewModel Canvas { get; set; }

        private readonly IEventAggregator _eventAggregator;
        public IEventAggregator EventAggregator => _eventAggregator;

        // 这里是真正的数据源
        //public BindableCollection<ROI> RoiGroups { get; } = new BindableCollection<ROI>();
        public BindableCollection<ROIRegion> FlatRegions { get; } = new BindableCollection<ROIRegion>();

        public RoiMainViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            ToolBar = new ToolBarViewModel(eventAggregator);
            Canvas = new CanvasViewModel(eventAggregator);
            Canvas.FlatROIRegions = FlatRegions;
            Items.Add(ToolBar);
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
