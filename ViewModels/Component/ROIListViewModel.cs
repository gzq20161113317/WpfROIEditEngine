using Caliburn.Micro;
using RoiEditor.Core.Undo;
using RoiEditor.Core.Undo.Commands;
using RoiEditor.Enums;
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
        private readonly UndoManager _undoManager;
        private ROI _selectedROI;

        public ROIListViewModel(IEventAggregator eventAggregator, IWindowManager windowManager, UndoManager undoManager)
        {
            _eventAggregator = eventAggregator;
            _windowManager = windowManager;
            _undoManager = undoManager;
            ROIS = new ObservableCollection<ROI>();
        }

        /// <summary>
        /// 上帝集合！！！
        /// </summary>
        public ObservableCollection<ROI> ROIS { get; }

        /// <summary>
        /// UndoManager（暴露给外部，用于快捷键绑定）
        /// </summary>
        public UndoManager UndoManager => _undoManager;

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
                // 使用 Undo 命令
                var command = new CreateROICommand(ROIS, vm.ResultROI, _eventAggregator);
                _undoManager.ExecuteCommand(command);

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

                // ==========================================================
                // 步骤 1: 记录"案发现场" (获取被删除项的索引)
                // ==========================================================
                int index = ROIS.IndexOf(roi);

                // ==========================================================
                // 步骤 2: 安全辞职 (先清空 SelectedROI，防止幽灵数据)
                // ==========================================================
                if (SelectedROI == roi)
                {
                    // 注意：这里设为 null 是为了切断 Canvas 的引用，
                    // 稍后我们会马上赋予一个新的 SelectedROI，所以界面闪烁几乎不可见
                    SelectedROI = null;
                }

                // ==========================================================
                // 步骤 3: 使用 Undo 命令删除
                // ==========================================================
                var command = new DeleteROICommand(ROIS, roi, _eventAggregator);
                _undoManager.ExecuteCommand(command);

                // ==========================================================
                // 步骤 4: 善后处理 (自动选中 or 切换模式)
                // ==========================================================
                if (ROIS.Count > 0)
                {
                    // A. 还有剩余 ROI -> 自动选中"上一个"

                    // 逻辑解释：
                    // 如果删除了 index=2 (第3个)，我们希望选中 index=1 (第2个)。
                    // 如果删除了 index=0 (第1个)，我们希望选中 index=0 (新的第1个)。
                    int newIndex = index - 1;

                    // 兜底：不能小于 0
                    if (newIndex < 0) newIndex = 0;

                    // 执行选中
                    if (newIndex < ROIS.Count)
                    {
                        SelectedROI = ROIS[newIndex];
                    }
                }
                else
                {
                    // B. 删光了 -> 自动切换到 Pan (拖拽/浏览) 模式
                    // 这样用户删完最后一个 ROI 后，鼠标左键就可以直接拖动地图了，体验很丝滑

                    // 【注意】请确认你的枚举名是 ROIOperationMode.Pan 还是其他 (如 Drag)
                    _eventAggregator.PublishOnUIThread(new ROIOperationModeChangedEvent(ROIOperationMode.ROI_OS_Pan));
                }

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

        // 【新增】清空所有 ROI
        public void ClearAll()
        {
            if (ROIS.Count == 0) return;

            var result = MessageBox.Show(
                $"Are you sure you want to clear all {ROIS.Count} ROI(s)?\nThis action can be undone with Ctrl+Z.",
                "Confirm Clear All",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // 使用 Undo 命令
                var command = new ClearAllROICommand(ROIS, _eventAggregator);
                _undoManager.ExecuteCommand(command);

                // 清空选中
                SelectedROI = null;

                // 注意：切换到 Pan 模式的逻辑已经在 ClearAllROICommand.Execute() 中处理
            }
        }


    }
}
