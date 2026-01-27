using Caliburn.Micro;
using RoiEditor.Enums;
using RoiEditor.Events;
using RoiEditor.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RoiEditor.ViewModels
{
    public class RoiMainViewModel : Screen
    {
        private readonly IEventAggregator _eventAggregator;
        private DrawMode _currentMode = DrawMode.Pan;
        private string _mapPath;
        private int _currentLevel;
        private int _maxLevel;

        private ROIRegion _selectedROIRegion;

        public BindableCollection<ROIRegion> ROIRegionList { get; set; } = new BindableCollection<ROIRegion>();

        // 用于界面显示的格式化字符串
        public string LevelStatusString => $"Layer: {CurrentLevel} / {MaxLevel}";

        public RoiMainViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;

        }

        public IEventAggregator EventAggregator => _eventAggregator;

        public string MapPath
        {
            get => _mapPath;
            set { _mapPath = value; NotifyOfPropertyChange(() => MapPath); }
        }

        public DrawMode CurrentMode
        {
            get => _currentMode;
            set { _currentMode = value; NotifyOfPropertyChange(() => CurrentMode); }
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

        // 外部调用方法
        public void SetPanMode() => CurrentMode = DrawMode.Pan;
        public void SetSelectMode() => CurrentMode = DrawMode.Select;
        public void SetDrawRectMode() => CurrentMode = DrawMode.DrawRectangle;

        public void LoadMap(string path)
        {
            MapPath = path;
        }

        public void FitToCoarsest()
        {
            Execute.OnUIThread(() =>
            {
                _eventAggregator.PublishOnCurrentThread(new FitToCoarsestRequestEvent());
            });
        }

        protected override void OnDeactivate(bool close)
        {
            base.OnDeactivate(close);
        }
    }
}
