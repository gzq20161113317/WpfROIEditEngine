using RoiEditor.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace RoiEditor.Core.Undo.Commands
{
    /// <summary>
    /// 调整 Region 大小命令
    /// </summary>
    public class ResizeRegionCommand : UndoableCommandBase
    {
        private readonly ROIRegion _region;
        private readonly List<Point> _oldPoints;
        private readonly List<Point> _newPoints;
        private readonly DateTime _timestamp;

        public override string Description => "Resize Region";

        public ResizeRegionCommand(ROIRegion region, List<Point> oldPoints, List<Point> newPoints)
        {
            _region = region;
            _oldPoints = new List<Point>(oldPoints);
            _newPoints = new List<Point>(newPoints);
            _timestamp = DateTime.Now;
        }

        public override void Execute()
        {
            if (_region.Points != null)
            {
                _region.Points.Clear();
                foreach (var p in _newPoints)
                {
                    _region.Points.Add(p);
                }
                _region.NotifyOfPropertyChange("Points");
            }
        }

        public override void Undo()
        {
            if (_region.Points != null)
            {
                _region.Points.Clear();
                foreach (var p in _oldPoints)
                {
                    _region.Points.Add(p);
                }
                _region.NotifyOfPropertyChange("Points");
            }
        }

        /// <summary>
        /// 支持合并连续的调整大小操作（拖拽手柄时）
        /// </summary>
        public override bool CanMerge(IUndoableCommand other)
        {
            if (other is ResizeRegionCommand resizeCmd)
            {
                // 只有在 500ms 内的连续调整才合并
                if ((resizeCmd._timestamp - _timestamp).TotalMilliseconds < 500)
                {
                    // 必须是同一个 Region
                    if (ReferenceEquals(_region, resizeCmd._region))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public override void Merge(IUndoableCommand other)
        {
            if (other is ResizeRegionCommand resizeCmd)
            {
                // 更新 newPoints 为最新的点
                _newPoints.Clear();
                _newPoints.AddRange(resizeCmd._newPoints);
            }
        }
    }
}
