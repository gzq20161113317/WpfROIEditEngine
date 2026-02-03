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
    /// 拆分 ROI 命令 - 将一个 ROI 的所有 Region 拆分成独立的 ROI
    /// </summary>
    public class SplitROICommand : UndoableCommandBase
    {
        private readonly ObservableCollection<ROI> _roiCollection;
        private readonly ROI _originalROI;
        private readonly int _originalIndex;
        private readonly List<ROI> _newROIs; // 拆分后创建的新 ROI 列表
        private readonly IEventAggregator _eventAggregator;

        public override string Description => $"Split ROI: {_originalROI.Name} ({_newROIs.Count} new ROIs)";

        public SplitROICommand(ObservableCollection<ROI> roiCollection, ROI originalROI, IEventAggregator eventAggregator = null)
        {
            _roiCollection = roiCollection;
            _originalROI = originalROI;
            _originalIndex = roiCollection.IndexOf(originalROI);
            _eventAggregator = eventAggregator;
            _newROIs = new List<ROI>();
        }

        public override void Execute()
        {
            // 1. 为每个 Region 创建新的 ROI
            int counter = 1;
            var regionsToSplit = _originalROI.Regions.ToList(); // 复制列表，避免在遍历时修改

            foreach (var region in regionsToSplit)
            {
                // 生成唯一名字
                string newName = GenerateUniqueName($"{_originalROI.Name}_{counter}");
                counter++;

                // 创建新 ROI
                var newROI = new ROI
                {
                    Name = newName,
                    Color = _originalROI.Color,
                    IsVisible = _originalROI.IsVisible
                };

                // 将 Region 从原 ROI 移动到新 ROI
                _originalROI.Regions.Remove(region);
                newROI.Regions.Add(region);

                // 添加到集合
                _roiCollection.Add(newROI);
                _newROIs.Add(newROI);
            }

            // 2. 删除原 ROI
            _roiCollection.Remove(_originalROI);

            // 3. 如果列表为空，切换到 Pan 模式
            CheckAndSwitchToPanIfEmpty();
        }

        public override void Undo()
        {
            // 1. 删除所有新创建的 ROI，并将 Region 移回原 ROI
            foreach (var newROI in _newROIs)
            {
                // 将 Region 移回原 ROI
                var region = newROI.Regions.FirstOrDefault();
                if (region != null)
                {
                    newROI.Regions.Remove(region);
                    _originalROI.Regions.Add(region);
                }

                // 删除新 ROI
                _roiCollection.Remove(newROI);
            }

            // 2. 恢复原 ROI 到原来的位置
            if (_originalIndex >= 0 && _originalIndex <= _roiCollection.Count)
            {
                _roiCollection.Insert(_originalIndex, _originalROI);
            }
            else
            {
                _roiCollection.Add(_originalROI);
            }

            // 3. 检查是否需要切换到 Pan 模式
            CheckAndSwitchToPanIfEmpty();
        }

        public override void Redo()
        {
            // 清空 _newROIs 列表，因为 Execute 会重新填充
            _newROIs.Clear();
            base.Redo(); // 调用 Execute()
        }

        private void CheckAndSwitchToPanIfEmpty()
        {
            if (_roiCollection.Count == 0 && _eventAggregator != null)
            {
                _eventAggregator.PublishOnUIThread(new ROIOperationModeChangedEvent(ROIOperationMode.ROI_OS_Pan));
            }
        }

        // 生成唯一名字（避免重名）
        private string GenerateUniqueName(string baseName)
        {
            string newName = baseName;
            int counter = 1;

            while (_roiCollection.Any(r => r.Name == newName))
            {
                newName = $"{baseName}_{counter}";
                counter++;
            }

            return newName;
        }
    }
}
