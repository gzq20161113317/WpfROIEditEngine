using Caliburn.Micro;
using RoiEditor.Core.Undo;
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
using System.Windows.Input;

namespace RoiEditor.ViewModels
{
    public class RoiMainViewModel : Conductor<IScreen>.Collection.AllActive
    {

        private readonly IEventAggregator _eventAggregator;
        private readonly IWindowManager _windowManager;
        private readonly UndoManager _undoManager;

        //公开子模块给View使用
        public ToolBarViewModel ToolBar { get; set; }
        public CanvasViewModel Canvas { get; set; }
        public ROIListViewModel ROIListVM { get; set; }
        public SettingViewModel SettingVM { get; set; }


        public IEventAggregator EventAggregator => _eventAggregator;
        public UndoManager UndoManager => _undoManager;

        // Undo/Redo 命令（用于快捷键绑定）
        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }

        public RoiMainViewModel(IEventAggregator eventAggregator,IWindowManager windowManager)
        {
            _eventAggregator = eventAggregator;
            _windowManager = windowManager;
            _undoManager = new UndoManager(maxStackSize: 100);

            // 初始化命令（简化版，不检查 CanExecute）
            UndoCommand = new RelayCommand(_ =>
            {
                if (_undoManager.CanUndo)
                    _undoManager.Undo();
            });

            RedoCommand = new RelayCommand(_ =>
            {
                if (_undoManager.CanRedo)
                    _undoManager.Redo();
            });

            //初始化所有子组件
            ToolBar = new ToolBarViewModel(eventAggregator, _undoManager);
            ROIListVM = new ROIListViewModel(eventAggregator,windowManager, _undoManager);
            SettingVM = new SettingViewModel(eventAggregator);
            Canvas = new CanvasViewModel(eventAggregator);

            Canvas.ROICollection = ROIListVM.ROIS;

            // 将 ROIListVM 传递给 ToolBar，用于 ClearROIS 操作
            ToolBar.ROIListVM = ROIListVM;

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
