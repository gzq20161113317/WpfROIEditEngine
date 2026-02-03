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
    public class SelectSettingViewModel : Screen,
        IHandle<ActiveROIChangedEvent>,
        IHandle<MapInfoChangedEvent>
    {
        private readonly IEventAggregator _eventAggregator;
        private ROI _currentActiveROI;
        private ROI _previousActiveROI; // 保存上一个 ROI，用于恢复默认线宽

        // === 边界检查 ===
        private double _minX = 0;
        private double _minY = 0;
        private double _maxX = double.MaxValue;
        private double _maxY = double.MaxValue;

        // === 常量 ===
        private const double DEFAULT_LINE_WIDTH = 1.0; // 默认线宽

        // === 绑定属性 ===
        private double _targetLineWidth = 1.0;
        private double _moveStep = 10.0;
        private double _inflateAmount = 1.0;
        public double TargetLineWidth
        {
            get => _targetLineWidth;
            set
            {
                if (_targetLineWidth != value)
                {
                    _targetLineWidth = value;
                    NotifyOfPropertyChange(() => TargetLineWidth);

                    // 调试输出
                    System.Diagnostics.Debug.WriteLine($"[SelectSetting] TargetLineWidth changed to: {value}");

                    // 自动应用到当前 ActiveROI
                    ApplyLineWidthToCurrentROI();
                }
            }
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
            // 1. 恢复上一个 ROI 的默认线宽
            if (_previousActiveROI != null && _previousActiveROI.Regions != null)
            {
                foreach (var region in _previousActiveROI.Regions)
                {
                    region.LineWidth = DEFAULT_LINE_WIDTH;
                }
            }

            // 2. 更新当前 ROI
            _previousActiveROI = _currentActiveROI;
            _currentActiveROI = message.ActiveROI;

            // 3. 应用当前线宽到新的 ActiveROI
            ApplyLineWidthToCurrentROI();
        }

        public void Handle(MapInfoChangedEvent message)
        {
            // 更新边界限制
            _minX = message.MapBounds.Left;
            _minY = message.MapBounds.Top;
            _maxX = message.MapBounds.Right;
            _maxY = message.MapBounds.Bottom;
        }

        // === 功能 1: 应用线宽 (对 ActiveROI 下的所有 Region) ===
        public void ApplyLineWidth()
        {
            ApplyLineWidthToCurrentROI();
        }

        // 内部方法：应用线宽到当前 ROI
        private void ApplyLineWidthToCurrentROI()
        {
            if (_currentActiveROI == null || _currentActiveROI.Regions == null)
            {
                System.Diagnostics.Debug.WriteLine("[SelectSetting] No active ROI or regions");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[SelectSetting] Applying LineWidth {TargetLineWidth} to {_currentActiveROI.Regions.Count} regions");

            foreach (var region in _currentActiveROI.Regions)
            {
                region.LineWidth = TargetLineWidth;
                System.Diagnostics.Debug.WriteLine($"[SelectSetting] Set region {region.Id} LineWidth to {TargetLineWidth}");
            }
            // 不需要手动刷新，ROIRegion 属性变更会触发 PropertyChanged，Canvas 需要监听并重绘
        }

        // === 功能 2: 移动 (对选中的 Regions) ===
        // 方向: Left, Right, Up, Down
        public void Move(string direction)
        {
            if (_currentActiveROI == null) return;

            // 1. 优先获取明确选中的 Region
            var targetRegions = _currentActiveROI.Regions.Where(r => r.IsSelected).ToList();

            // 2. 【核心修改】如果没有选中任何 Region，但 ActiveROI 下有东西，默认操作整个 ActiveROI
            if (targetRegions.Count == 0 && _currentActiveROI.Regions.Count > 0)
            {
                targetRegions = _currentActiveROI.Regions.ToList();
            }

            // 3. 实在没有操作对象才退出
            if (targetRegions.Count == 0) return;

            Vector offset = new Vector(0, 0);
            switch (direction.ToLower())
            {
                case "left": offset.X = -MoveStep; break;
                case "right": offset.X = MoveStep; break;
                case "up": offset.Y = -MoveStep; break; // WPF Y轴向下为正
                case "down": offset.Y = MoveStep; break;
            }

            // 4. 改为遍历 targetRegions
            foreach (var region in targetRegions)
            {
                if (region.Points == null) continue;
                var newPoints = new List<Point>(region.Points.Count);
                bool isOutOfBounds = false;
                foreach (var p in region.Points)
                {
                    Point nextP = p + offset;
                    newPoints.Add(nextP);

                    // 检查单个点是否越界
                    if (!IsValidPoint(nextP))
                    {
                        isOutOfBounds = true;
                        break; // 只要有一个点越界，整个形状就不允许移动
                    }
                }

                // 只有在不越界的情况下才应用
                if (!isOutOfBounds)
                {
                    region.Points = newPoints;
                }
            }
        }

        // === 功能 3: 膨胀/内缩 (对选中的 Regions) ===
        // IsInflate: true=膨胀, false=内缩
        public void Inflate(bool isInflate)
        {
            if (_currentActiveROI == null) return;

            // 1. 同样的逻辑：优先选中的，没有则全选
            var targetRegions = _currentActiveROI.Regions.Where(r => r.IsSelected).ToList();

            // 2. 回退策略
            if (targetRegions.Count == 0 && _currentActiveROI.Regions.Count > 0)
            {
                targetRegions = _currentActiveROI.Regions.ToList();
            }

            if (targetRegions.Count == 0) return;

            double amount = isInflate ? InflateAmount : -InflateAmount;

            // 3. 改为遍历 targetRegions
            foreach (var region in targetRegions)
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

            // 统一边界检查
            if (newPoints != null)
            {
                bool isOutOfBounds = false;
                foreach (var p in newPoints)
                {
                    if (!IsValidPoint(p))
                    {
                        isOutOfBounds = true;
                        break;
                    }
                }

                // 只有所有点都在界内，才赋值
                if (!isOutOfBounds)
                {
                    region.Points = newPoints;
                }
            }
        }

        // === 辅助方法 ===
        private bool IsValidPoint(Point p)
        {
            // 至少不能小于 0
            if (p.X < _minX || p.Y < _minY) return false;

            // 如果你有地图的最大宽/高，可以在这里启用
            if (p.X > _maxX || p.Y > _maxY) return false;

            return true;
        } 
    }
}
