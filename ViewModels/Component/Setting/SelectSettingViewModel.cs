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
                for (int i = 0; i < region.Points.Count; i++)
                {
                    region.Points[i] += offset;
                }
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

            //TODO:这里采用最简单的做法（仅适用于简单凸多边形），后续需要引入Clipper库
            
            //1.计算中心
            double cx = 0,cy = 0;
            foreach (var p in region.Points) { cx += p.X; cy += p.Y; }
            Point center = new Point(cx / region.Points.Count, cy / region.Points.Count);

            // 2. 针对矩形做特殊优化 (精准扩边)
            if (region.Type == ROIRegionType.Rectangle && region.Points.Count == 4)
            {
                // 重新计算包围盒并扩大
                // 注意：这里假设点序是 TL, TR, BR, BL 或类似
                // 简单起见，我们算出 Bounds，扩大 Bounds，再写回 Points
                // 但这样会丢失旋转信息(如果有)。目前编辑器只支持正交矩形，所以安全。
                Rect bounds = new Rect(region.Points[0], region.Points[2]); // 假设是对角
                // 更严谨的 Bounds 计算
                double minX = region.Points.Min(p => p.X);
                double minY = region.Points.Min(p => p.Y);
                double maxX = region.Points.Max(p => p.X);
                double maxY = region.Points.Max(p => p.Y);

                // 向外扩大 amount
                minX -= amount; minY -= amount;
                maxX += amount; maxY += amount;

                // 检查是否缩没了
                if (maxX <= minX || maxY <= minY) return;

                // 重建点 (保持顺序: TL, TR, BR, BL)
                region.Points[0] = new Point(minX, minY);
                region.Points[1] = new Point(maxX, minY);
                region.Points[2] = new Point(maxX, maxY);
                region.Points[3] = new Point(minX, maxY);
            }
            else
            {
                // 其他形状：简单粗暴的中心放射移动 (不完美，但能用)
                for (int i = 0; i < region.Points.Count; i++)
                {
                    Vector dir = region.Points[i] - center;
                    // 归一化方向
                    if (dir.Length > 0.001)
                    {
                        dir.Normalize();
                        region.Points[i] += dir * amount;
                    }
                }
            }
        }
    }
}
