using RoiEditor.Core.Attributes;
using RoiEditor.Enums;
using RoiEditor.Models;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RoiEditor.Core.Interaction
{
    [RoiTool(ROIOperationMode.ROI_OS_ROI_Shape_Rectangle)]
    public class CreateRectTool : ToolBase
    {
        #region Fields
        private ROIRegion _newItem;//当前正在画的那个框
        private Point _startPoint;//鼠标按下的起点
        private bool _isCreating;//是否正在拖拽中
        #endregion


        public CreateRectTool(Controls.RoiEditorCanvas canvas) : base(canvas) { }

        #region Cursor & UI(光标逻辑)
        // 每次激活工具时，给 Canvas 一个崭新的 UI，绝对安全
        public override UIElement GetCustomCursorView() => CreateCrossCursorUI();

        // 创建一个十字准星光标，带白色描边，防止在黑色背景看不清
        private Grid CreateCrossCursorUI()
        {
            var grid = new Grid { Margin = new Thickness(-10, -10, 0, 0) };

            var pathDataStr = "M10,0 L10,20 M0,10 L20,10";
            var geometry = Geometry.Parse(pathDataStr);
            // 冻结资源以提升性能
            if (geometry.CanFreeze) geometry.Freeze();

            // 底层：白色描边 (3像素粗)
            var outlinePath = new Path
            {
                Data = geometry,
                Stroke = Brushes.White,
                StrokeThickness = 3,
                // 【关键】对于小尺寸光标，开启像素对齐能防止线条发虚
                SnapsToDevicePixels = true,
                IsHitTestVisible = false
            };



            // 先加描边，再加核心
            grid.Children.Add(outlinePath);

            return grid;
        }


        #endregion


        #region Mouse Interactions(交互核心)
        public override void OnMouseDown(MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            // 1. 校验环境
            if (_canvas.ActiveROI == null)
            {
                MessageBox.Show("Please select or create an ROI first.", "Warning");
                return;
            }

            var wPos = GetWorldPosition(e);

            // 校验有效区
            if (!_canvas.ValidRegion.IsEmpty && !_canvas.ValidRegion.Contains(wPos))
                return;

            // 2. 准备数据
            _startPoint = wPos;
            _newItem = new ROIRegion
            {
                Id = Guid.NewGuid(),
                Name = $"Rect {_canvas.ActiveROI.Regions.Count + 1}",
                Type = ROIRegionType.Rectangle,
                Points = new List<Point> { _startPoint, _startPoint, _startPoint, _startPoint },
                IsSelected = true,
                Parent = _canvas.ActiveROI // 【关键】显式设置父级，防止断链
            };

            // 3. 【关键修改】先开启交互状态！确保 OnMouseMove 能跑起来
            _isCreating = true;
            _canvas.CaptureMouse();

            // 4. 再进行数据绑定 (即使这里出小问题，也不会导致鼠标“不跟手”)
            try
            {
                _canvas.ActiveROI.Regions.Add(_newItem);
                _canvas.SelectROIRegion(_newItem);
            }
            catch (Exception ex)
            {
                // 如果出错，回滚状态
                _isCreating = false;
                _canvas.ReleaseMouseCapture();
                _newItem = null;
                MessageBox.Show($"Error creating ROI: {ex.Message}");
            }
        }

        public override void OnMouseMove(MouseEventArgs e)
        {
            // 双重保险：状态不对 或 对象为空 都不执行
            if (!_isCreating || _newItem == null) return;

            var wPos = GetWorldPosition(e);
            wPos = _canvas.ClampToValidRegion(wPos);

            // 更新矩形坐标
            _newItem.Points[1] = new Point(wPos.X, _startPoint.Y);
            _newItem.Points[2] = new Point(wPos.X, wPos.Y);
            _newItem.Points[3] = new Point(_startPoint.X, wPos.Y);

            // 【关键修改】手动通知属性变化，让 ViewModel 知道 Points 变了 -> 更新面积统计
            _newItem.NotifyOfPropertyChange("Points");

            // 强制重绘
            _canvas.RedrawEditorLayer();
        }

        public override void OnMouseUp(MouseButtonEventArgs e)
        {
            if (!_isCreating) return;

            _isCreating = false;
            _canvas.ReleaseMouseCapture();

            if (_newItem != null)
            {
                // 1. 规范化
                NormalizeRectPoints(_newItem.Points);

                // 2. 垃圾过滤
                Rect bounds = GetBoundingRect(_newItem.Points);
                if (bounds.Width < 5.0 || bounds.Height < 5.0)
                {
                    // 撤销
                    if (_canvas.ActiveROI != null)
                        _canvas.ActiveROI.Regions.Remove(_newItem);

                    _canvas.SelectROIRegion(null);
                }
                else
                {
                    // 3. 提交
                    _canvas.RebuildSpatialIndex();
                    // 这里不需要再 NotifyOfPropertyChange，因为上面已经 Normalize 过了
                }

                // 4. 最终刷新
                _canvas.RedrawEditorLayer();
                _newItem = null;
            }
        }
        #endregion

        #region Helpers
        private void NormalizeRectPoints(List<Point> pts)
        {
            if (pts == null || pts.Count < 4) return;

            double left = Math.Min(pts[0].X, pts[2].X);
            double right = Math.Max(pts[0].X, pts[2].X);
            double top = Math.Min(pts[0].Y, pts[2].Y);
            double bottom = Math.Max(pts[0].Y, pts[2].Y);

            pts[0] = new Point(left, top);
            pts[1] = new Point(right, top);
            pts[2] = new Point(right, bottom);
            pts[3] = new Point(left, bottom);
        }

        private Rect GetBoundingRect(IList<Point> points)
        {
            if (points == null || points.Count == 0) return Rect.Empty;
            return new Rect(points[0], points[2]); // 因为已经Normalize过了，0和2就是对角点
        }
        #endregion
    }
}