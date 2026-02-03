using Caliburn.Micro;
using RoiEditor.Enums;
using RoiEditor.Events;
using RoiEditor.Models;
using System.Collections.Generic;
using System.Linq;

namespace RoiEditor.Core.Undo.Commands
{
    /// <summary>
    /// 批量删除 Region 命令
    /// </summary>
    public class DeleteMultipleRegionsCommand : UndoableCommandBase
    {
        private readonly ROI _roi;
        private readonly List<RegionBackup> _backups; // 备份删除的 Region 及其索引
        private readonly IEventAggregator _eventAggregator;

        public override string Description => $"Delete {_backups.Count} Region(s)";

        // 内部类：保存 Region 和它的原始索引
        private class RegionBackup
        {
            public ROIRegion Region { get; set; }
            public int Index { get; set; }
        }

        public DeleteMultipleRegionsCommand(ROI roi, List<ROIRegion> regionsToDelete, IEventAggregator eventAggregator = null)
        {
            _roi = roi;
            _eventAggregator = eventAggregator;
            _backups = new List<RegionBackup>();

            // 备份每个 Region 及其索引
            foreach (var region in regionsToDelete)
            {
                int index = _roi.Regions.IndexOf(region);
                if (index >= 0)
                {
                    _backups.Add(new RegionBackup { Region = region, Index = index });
                }
            }
        }

        public override void Execute()
        {
            // 删除所有 Region
            foreach (var backup in _backups)
            {
                _roi.Regions.Remove(backup.Region);
            }

            // 如果 ROI 的所有 Region 都被删除了，切换到 Pan 模式
            CheckAndSwitchToPanIfEmpty();
        }

        public override void Undo()
        {
            // 按原始索引恢复所有 Region
            // 注意：需要按索引从小到大排序，确保恢复顺序正确
            var sortedBackups = _backups.OrderBy(b => b.Index).ToList();

            foreach (var backup in sortedBackups)
            {
                if (backup.Index >= 0 && backup.Index <= _roi.Regions.Count)
                {
                    _roi.Regions.Insert(backup.Index, backup.Region);
                }
                else
                {
                    _roi.Regions.Add(backup.Region);
                }
            }

            CheckAndSwitchToPanIfEmpty();
        }

        public override void Redo()
        {
            base.Redo(); // 调用 Execute()
        }

        private void CheckAndSwitchToPanIfEmpty()
        {
            if (_roi.Regions.Count == 0 && _eventAggregator != null)
            {
                _eventAggregator.PublishOnUIThread(new ROIOperationModeChangedEvent(ROIOperationMode.ROI_OS_Pan));
            }
        }
    }
}
