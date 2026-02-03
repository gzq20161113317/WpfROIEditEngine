using Caliburn.Micro;
using RoiEditor.Enums;
using RoiEditor.Events;
using RoiEditor.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RoiEditor.Core.Undo.Commands
{
    /// <summary>
    /// 清空所有 ROI 命令
    /// </summary>
    public class ClearAllROICommand : UndoableCommandBase
    {
        private readonly ObservableCollection<ROI> _roiCollection;
        private readonly List<ROI> _backup; // 备份所有 ROI
        private readonly IEventAggregator _eventAggregator;

        public override string Description => $"Clear All ROI ({_backup.Count} items)";

        public ClearAllROICommand(ObservableCollection<ROI> roiCollection, IEventAggregator eventAggregator = null)
        {
            _roiCollection = roiCollection;
            // 备份当前所有 ROI（深拷贝引用，不是克隆对象）
            _backup = roiCollection.ToList();
            _eventAggregator = eventAggregator;
        }

        public override void Execute()
        {
            _roiCollection.Clear();

            // 清空后自动切换到 Pan 模式
            CheckAndSwitchToPanIfEmpty();
        }

        public override void Undo()
        {
            // 恢复所有 ROI
            foreach (var roi in _backup)
            {
                _roiCollection.Add(roi);
            }

            // Undo 后也检查：如果列表为空，切换到 Pan 模式
            CheckAndSwitchToPanIfEmpty();
        }

        public override void Redo()
        {
            base.Redo(); // 调用 Execute()

            // Redo 后也检查：如果列表为空，切换到 Pan 模式
            CheckAndSwitchToPanIfEmpty();
        }

        private void CheckAndSwitchToPanIfEmpty()
        {
            if (_roiCollection.Count == 0 && _eventAggregator != null)
            {
                _eventAggregator.PublishOnUIThread(new ROIOperationModeChangedEvent(ROIOperationMode.ROI_OS_Pan));
            }
        }
    }
}
