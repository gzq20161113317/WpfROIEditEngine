using Caliburn.Micro;
using RoiEditor.Enums;
using RoiEditor.Events;
using RoiEditor.Models;
using System.Collections.ObjectModel;

namespace RoiEditor.Core.Undo.Commands
{
    /// <summary>
    /// 删除 ROI 命令
    /// </summary>
    public class DeleteROICommand : UndoableCommandBase
    {
        private readonly ObservableCollection<ROI> _roiCollection;
        private readonly ROI _roi;
        private readonly int _index; // 记录原来的位置，以便恢复时插入到正确位置
        private readonly IEventAggregator _eventAggregator;

        public override string Description => $"Delete ROI: {_roi.Name}";

        public DeleteROICommand(ObservableCollection<ROI> roiCollection, ROI roi, IEventAggregator eventAggregator = null)
        {
            _roiCollection = roiCollection;
            _roi = roi;
            _index = roiCollection.IndexOf(roi);
            _eventAggregator = eventAggregator;
        }

        public override void Execute()
        {
            _roiCollection.Remove(_roi);

            // 如果删除后列表为空，自动切换到 Pan 模式
            CheckAndSwitchToPanIfEmpty();
        }

        public override void Undo()
        {
            // 恢复到原来的位置
            if (_index >= 0 && _index <= _roiCollection.Count)
            {
                _roiCollection.Insert(_index, _roi);
            }
            else
            {
                _roiCollection.Add(_roi);
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
