using Caliburn.Micro;
using RoiEditor.Enums;
using RoiEditor.Events;
using RoiEditor.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace RoiEditor.ViewModels.Component.Setting
{
    public class SelectSettingViewModel : Screen,IHandle<ActiveROIChangedEvent>
    {
        private readonly IEventAggregator _eventAggregator;
        private ROI _currentActiveROI;

        // === 绑定属性 ===
        private double _targetLineWidth = 1.0;
        private double _moveStep = 10.0;
        private double _inflateAmount = 1.0;
        public double TargetLineWidth
        {
            get => _targetLineWidth;
            set { _targetLineWidth = value; NotifyOfPropertyChange(() => TargetLineWidth); }
        }

        public double MoveStep
        {
            get => _moveStep;
            set { _moveStep = value; NotifyOfPropertyChange(() => MoveStep); }
        }

        public double InflateAmount
        {
            get => _inflateAmount;
            set { _inflateAmount = value; NotifyOfPropertyChange(() => InflateAmount); }
        }

        public SelectSettingViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _eventAggregator.Subscribe(this);
        }

        // === 事件处理：获取当前激活的 ROI ===
        public void Handle(ActiveROIChangedEvent message)
        {
            _currentActiveROI = message.ActiveROI;
        }

        // === 功能 1: 应用线宽 (对 ActiveROI 下的所有 Region) ===
        public void ApplyLineWidth()
        {
            if (_currentActiveROI == null || _currentActiveROI.Regions == null) return;

            foreach (var region in _currentActiveROI.Regions)
            {
                region.LineWidth = TargetLineWidth;
            }
            // 不需要手动刷新，ROIRegion 属性变更会触发 PropertyChanged，Canvas 需要监听并重绘
        }

        // === 功能 2: 移动 (对选中的 Regions) ===
        // 方向: Left, Right, Up, Down
        public void Move(string direction)
        {
            if (_currentActiveROI == null) return;

            // 获取选中的 Region
            var selectedRegions = _currentActiveROI.Regions.Where(r => r.IsSelected).ToList();
            if (selectedRegions.Count == 0) return;

            Vector offset = new Vector(0, 0);
            switch (direction.ToLower())
            {
                case "left": offset.X = -MoveStep; break;
                case "right": offset.X = MoveStep; break;
                case "up": offset.Y = -MoveStep; break; // WPF Y轴向下为正
                case "down": offset.Y = MoveStep; break;
            }

            foreach (var region in selectedRegions)
            {
                if (region.Points == null) continue;
                var newPoints = new List<Point>(region.Points.Count);
                foreach (var p in region.Points)
                {
                    newPoints.Add(p + offset);
                }

                // 赋值回去，ROIRegion.Points 的 Setter 会被调用 -> 触发 Canvas 重绘
                region.Points = newPoints;
            }
        }

        // === 功能 3: 膨胀/内缩 (对选中的 Regions) ===
        // IsInflate: true=膨胀, false=内缩
        public void Inflate(bool isInflate)
        {
            if (_currentActiveROI == null) return;
            var selectedRegions = _currentActiveROI.Regions.Where(r => r.IsSelected).ToList();
            if (selectedRegions.Count == 0) return;

            double amount = isInflate ? InflateAmount : -InflateAmount;

            foreach (var region in selectedRegions)
            {
                InflateRegion(region, amount);
            }
        }

        private void InflateRegion(ROIRegion region,double amount)
        {
            if (region.Points == null || region.Points.Count == 0) return;

            // 临时存储新点
            List<Point> newPoints = null;

            //TODO:需要引入Clipper做复杂的计算才能够使用这个功能，此处仅演示作用
            // 1. 矩形特殊处理
            if (region.Type == ROIRegionType.Rectangle && region.Points.Count == 4)
            {
                double minX = region.Points.Min(p => p.X);
                double minY = region.Points.Min(p => p.Y);
                double maxX = region.Points.Max(p => p.X);
                double maxY = region.Points.Max(p => p.Y);

                minX -= amount; minY -= amount;
                maxX += amount; maxY += amount;

                if (maxX <= minX || maxY <= minY) return;

                newPoints = new List<Point>
                {
                    new Point(minX, minY),
                    new Point(maxX, minY),
                    new Point(maxX, maxY),
                    new Point(minX, maxY)
                };
            }
            else
            {
                // 2. 其他形状 (简单中心膨胀)
                double cx = 0, cy = 0;
                foreach (var p in region.Points) { cx += p.X; cy += p.Y; }
                Point center = new Point(cx / region.Points.Count, cy / region.Points.Count);

                newPoints = new List<Point>(region.Points.Count);
                foreach (var p in region.Points)
                {
                    Vector dir = p - center;
                    if (dir.Length > 0.001)
                    {
                        dir.Normalize();
                        newPoints.Add(p + dir * amount);
                    }
                    else
                    {
                        newPoints.Add(p);
                    }
                }
            }

            // 赋值回去，触发通知
            if (newPoints != null)
            {
                region.Points = newPoints;
            }
        }
    }
}
