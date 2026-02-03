using RoiEditor.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace RoiEditor.Core.Undo.Commands
{
    /// <summary>
    /// 移动 Region 命令（支持多选）
    /// </summary>
    public class MoveRegionCommand : UndoableCommandBase
    {
        private readonly List<ROIRegion> _regions;
        private Vector _delta; // 改为可变字段
        private readonly DateTime _timestamp;

        public override string Description
        {
            get
            {
                if (_regions.Count == 1)
                    return $"Move Region ({_delta.X:F0}, {_delta.Y:F0})";
                else
                    return $"Move {_regions.Count} Regions ({_delta.X:F0}, {_delta.Y:F0})";
            }
        }

        public MoveRegionCommand(List<ROIRegion> regions, Vector delta)
        {
            _regions = regions;
            _delta = delta;
            _timestamp = DateTime.Now;
        }

        public override void Execute()
        {
            foreach (var region in _regions)
            {
                if (region.Points != null)
                {
                    for (int i = 0; i < region.Points.Count; i++)
                    {
                        region.Points[i] += _delta;
                    }
                    region.NotifyOfPropertyChange("Points");
                }
            }
        }

        public override void Undo()
        {
            foreach (var region in _regions)
            {
                if (region.Points != null)
                {
                    for (int i = 0; i < region.Points.Count; i++)
                    {
                        region.Points[i] -= _delta;
                    }
                    region.NotifyOfPropertyChange("Points");
                }
            }
        }

        /// <summary>
        /// 支持合并连续的移动操作（拖拽时）
        /// </summary>
        public override bool CanMerge(IUndoableCommand other)
        {
            if (other is MoveRegionCommand moveCmd)
            {
                // 只有在 500ms 内的连续移动才合并
                if ((moveCmd._timestamp - _timestamp).TotalMilliseconds < 500)
                {
                    // 必须是相同的 Region 集合
                    if (_regions.Count == moveCmd._regions.Count &&
                        _regions.All(r => moveCmd._regions.Contains(r)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public override void Merge(IUndoableCommand other)
        {
            if (other is MoveRegionCommand moveCmd)
            {
                // 累加 delta
                _delta = new Vector(_delta.X + moveCmd._delta.X, _delta.Y + moveCmd._delta.Y);
            }
        }
    }
}
