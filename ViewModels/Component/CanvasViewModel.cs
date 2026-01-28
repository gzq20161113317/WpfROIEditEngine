using Caliburn.Micro;
using RoiEditor.Enums;
using RoiEditor.Events;
using RoiEditor.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoiEditor.ViewModels.Component
{
    public class CanvasViewModel:Screen, IHandle<ROIOperationModeChangedEvent>
    {
        private readonly IEventAggregator _eventAggregator;
        private ROIOperationMode _currentOperationMode = ROIOperationMode.ROI_OS_Pan;
        private string _mapPath;
        private int _currentLevel;
        private int _maxLevel;
        private ROIRegion _selectedROIRegion;

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

        // 当前选中 ROI
        public ROIRegion SelectedROIRegion
        {
            get => _selectedROIRegion;
            set
            {
                if (ReferenceEquals(_selectedROIRegion, value)) return;
                _selectedROIRegion = value;
                NotifyOfPropertyChange(() => SelectedROIRegion);
            }
        }

        public void Handle(ROIOperationModeChangedEvent message)
        {
            CurrentOperationMode = message.CurrentOperationMode;
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
