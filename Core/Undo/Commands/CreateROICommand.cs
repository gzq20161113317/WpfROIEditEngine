using Caliburn.Micro;
using RoiEditor.Enums;
using RoiEditor.Events;
using RoiEditor.Models;
using System.Collections.ObjectModel;

namespace RoiEditor.Core.Undo.Commands
{
    /// <summary>
    /// 创建 ROI 命令
    /// </summary>
    public class CreateROICommand : UndoableCommandBase
    {
        private readonly ObservableCollection<ROI> _roiCollection;
        private readonly ROI _roi;
        private readonly IEventAggregator _eventAggregator;

        public override string Description => $"Create ROI: {_roi.Name}";

        public CreateROICommand(ObservableCollection<ROI> roiCollection, ROI roi, IEventAggregator eventAggregator = null)
        {
            _roiCollection = roiCollection;
            _roi = roi;
            _eventAggregator = eventAggregator;
        }

        public override void Execute()
        {
            _roiCollection.Add(_roi);
        }

        public override void Undo()
        {
            _roiCollection.Remove(_roi);

            // Undo 创建后，如果列表为空，自动切换到 Pan 模式
            CheckAndSwitchToPanIfEmpty();
        }

        public override void Redo()
        {
            base.Redo(); // 调用 Execute()
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
