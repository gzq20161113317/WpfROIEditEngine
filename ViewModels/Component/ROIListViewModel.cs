using Caliburn.Micro;
using RoiEditor.Events;
using RoiEditor.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace RoiEditor.ViewModels.Component
{
    public class ROIListViewModel : Screen
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IWindowManager _windowManager;
        private ROI _selectedROI;

        public ROIListViewModel(IEventAggregator eventAggregator, IWindowManager windowManager)
        {
            _eventAggregator = eventAggregator;
            _windowManager = windowManager;
            ROIS = new ObservableCollection<ROI>();
        }

        // 这里的 ROI 列表是“数据源头”
        public ObservableCollection<ROI> ROIS { get; }

        public ROI SelectedROI
        {
            get => _selectedROI;
            set
            {
                if (_selectedROI == value) return;

                // 处理旧选中状态
                if (_selectedROI != null) _selectedROI.IsSelected = false;

                _selectedROI = value;

                // 处理新选中状态
                if (_selectedROI != null) _selectedROI.IsSelected = true;

                NotifyOfPropertyChange(() => SelectedROI);

                // 发送事件，通知 ToolBar 等组件
                _eventAggregator.PublishOnUIThread(new ActiveROIChangedEvent(_selectedROI));
            }
        }

        public void AddNewROI()
        {
            var vm = new AddROIViewModel();
            var result = _windowManager.ShowDialog(vm);

            if (result == true && vm.ResultROI != null)
            {
                ROIS.Add(vm.ResultROI);
                SelectedROI = vm.ResultROI; // 自动选中新建项
            }
        }

        public void DisplayAll()
        {
            foreach (var roi in ROIS)
            {
                roi.IsVisible = true;
            }
        }

        public void DeleteRoi(ROI roi)
        {
            if (roi == null) return;

            var result = MessageBox.Show($"Are you sure you want to delete ROI '{roi.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                if (SelectedROI == roi) SelectedROI = null;
                ROIS.Remove(roi);
            }
        }

        // 【新增】重命名功能
        public void RenameRoi(ROI roi)
        {
            if (roi == null) return;

            // 复用 AddRoiViewModel 进行编辑，需要稍微改造 AddRoiViewModel 接收参数，
            // 或者简单起见，这里假设 AddRoiViewModel 可以设置初始值
            var vm = new AddROIViewModel();
            vm.ROIName = roi.Name;
            vm.SelectedColor = roi.Color;

            if (_windowManager.ShowDialog(vm) == true)
            {
                roi.Name = vm.ROIName;
                roi.Color = vm.SelectedColor;
            }
        }

        // 【新增】切换显隐 (点击眼睛)
        public void ToggleVisibility(ROI roi)
        {
            if (roi != null)
            {
                roi.IsVisible = !roi.IsVisible;
            }
        }


    }
}
