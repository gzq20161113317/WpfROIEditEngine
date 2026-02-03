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
    /// <summary>
    /// 两点矩形工具 - 通过两个点定义矩形（对角线）
    /// 与 CreateRectTool 的区别：可能支持旋转或其他特殊行为
    /// </summary>
    [RoiTool(ROIOperationMode.ROI_OS_ROI_Shape_TwoPoint)]
    public class CreateTwoPointRectTool : ToolBase
    {
        #region Fields
        private ROIRegion _newItem; // 当前正在画的矩形
        private Point _startPoint; // 第一个点
        private bool _isCreating; // 是否正在拖拽中
        #endregion

        public CreateTwoPointRectTool(Controls.RoiEditorCanvas canvas) : base(canvas) { }

        #region Cursor & UI
        public override UIElement GetCustomCursorView() => CreateCrossCursorUI();

        private Grid CreateCrossCursorUI()
        {
            var grid = new Grid { Margin = new Thickness(-10, -10, 0, 0) };

            var pathDataStr = "M10,0 L10,20 M0,10 L20,10";
            var geometry = Geometry.Parse(pathDataStr);
            if (geometry.CanFreeze) geometry.Freeze();

            var outlinePath = new Path
            {
                Data = geometry,
                Stroke = Brushes.Cyan,
                StrokeThickness = 3,
                SnapsToDevicePixels = true,
                IsHitTestVisible = false
            };

            grid.Children.Add(outlinePath);
            return grid;
        }
        #endregion

        #region Mouse Interactions
        public override void OnMouseDown(MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            // 校验环境
            if (_canvas.ActiveROI == null)
            {
                MessageBox.Show("Please select or create an ROI first.", "Warning");
                return;
            }

            var wPos = GetWorldPosition(e);

            // 校验有效区
            if (!_canvas.ValidRegion.IsEmpty && !_canvas.ValidRegion.Contains(wPos))
                return;

            // 记录起点
            _startPoint = wPos;

            _newItem = new ROIRegion
            {
                Id = Guid.NewGuid(),
                Name = $"TwoPointRect {_canvas.ActiveROI.Regions.Count + 1}",
                Type = ROIRegionType.Rectangle,
                Points = new List<Point> { _startPoint, _startPoint, _startPoint, _startPoint },
                IsSelected = true,
                Parent = _canvas.ActiveROI
            };

            _isCreating = true;
            _canvas.CaptureMouse();

            try
            {
                _canvas.ActiveROI.Regions.Add(_newItem);
                _canvas.SelectROIRegion(_newItem);
            }
            catch (Exception ex)
            {
                _isCreating = false;
                _canvas.ReleaseMouseCapture();
                _newItem = null;
                MessageBox.Show($"Error creating ROI: {ex.Message}");
            }
        }

        public override void OnMouseMove(MouseEventArgs e)
        {
            if (!_isCreating || _newItem == null) return;

            var wPos = GetWorldPosition(e);
            wPos = _canvas.ClampToValidRegion(wPos);

            // 更新矩形坐标（对角线两点定义）
            _newItem.Points[0] = _startPoint;
            _newItem.Points[1] = new Point(wPos.X, _startPoint.Y);
            _newItem.Points[2] = wPos;
            _newItem.Points[3] = new Point(_startPoint.X, wPos.Y);

            _newItem.NotifyOfPropertyChange("Points");
            _canvas.RedrawEditorLayer();
        }

        public override void OnMouseUp(MouseButtonEventArgs e)
        {
            if (!_isCreating) return;

            _isCreating = false;
            _canvas.ReleaseMouseCapture();

            if (_newItem != null)
            {
                // 规范化矩形点
                NormalizeRectPoints(_newItem.Points);

                // 垃圾过滤
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
                    // 提交
                    _canvas.RebuildSpatialIndex();
                }

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
            return new Rect(points[0], points[2]);
        }
        #endregion
    }
}
