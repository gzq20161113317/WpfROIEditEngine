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
    [RoiTool(ROIOperationMode.ROI_OS_ROI_Shape_Ellipse)]
    public class CreateEllipseTool : ToolBase
    {
        #region Fields
        private ROIRegion _newItem; // 当前正在画的椭圆
        private Point _centerPoint; // 中心点
        private bool _isCreating; // 是否正在拖拽中
        #endregion

        public CreateEllipseTool(Controls.RoiEditorCanvas canvas) : base(canvas) { }

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
                Stroke = Brushes.White,
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

            // 记录起点（左上角）
            _centerPoint = wPos;

            // 创建椭圆（使用两个点表示：左上角和右下角）
            _newItem = new ROIRegion
            {
                Id = Guid.NewGuid(),
                Name = $"Ellipse {_canvas.ActiveROI.Regions.Count + 1}",
                Type = ROIRegionType.Ellipse,
                Points = new List<Point> { wPos, wPos }, // 两个点：左上角和右下角
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

            System.Diagnostics.Debug.WriteLine($"[CreateEllipseTool] OnMouseMove: wPos={wPos}");

            // 更新右下角点（对角线定义椭圆）
            _newItem.Points[1] = wPos;
            _newItem.NotifyOfPropertyChange("Points");

            System.Diagnostics.Debug.WriteLine($"[CreateEllipseTool] After update: Points[0]={_newItem.Points[0]}, Points[1]={_newItem.Points[1]}");

            _canvas.RedrawEditorLayer();
        }

        public override void OnMouseUp(MouseButtonEventArgs e)
        {
            if (!_isCreating) return;

            _isCreating = false;
            _canvas.ReleaseMouseCapture();

            if (_newItem != null)
            {
                System.Diagnostics.Debug.WriteLine($"[CreateEllipseTool] OnMouseUp: Points[0]={_newItem.Points[0]}, Points[1]={_newItem.Points[1]}");

                // 计算边界框
                double left = Math.Min(_newItem.Points[0].X, _newItem.Points[1].X);
                double right = Math.Max(_newItem.Points[0].X, _newItem.Points[1].X);
                double top = Math.Min(_newItem.Points[0].Y, _newItem.Points[1].Y);
                double bottom = Math.Max(_newItem.Points[0].Y, _newItem.Points[1].Y);

                double width = right - left;
                double height = bottom - top;

                System.Diagnostics.Debug.WriteLine($"[CreateEllipseTool] Bounds: width={width}, height={height}");

                // 垃圾过滤（太小的椭圆）
                if (width < 5.0 || height < 5.0)
                {
                    System.Diagnostics.Debug.WriteLine($"[CreateEllipseTool] Ellipse too small, removing");
                    // 撤销
                    if (_canvas.ActiveROI != null)
                        _canvas.ActiveROI.Regions.Remove(_newItem);

                    _canvas.SelectROIRegion(null);
                }
                else
                {
                    // 规范化点（确保是左上角和右下角）- 直接修改列表元素
                    _newItem.Points[0] = new Point(left, top);
                    _newItem.Points[1] = new Point(right, bottom);

                    System.Diagnostics.Debug.WriteLine($"[CreateEllipseTool] Normalized Points: Points[0]={_newItem.Points[0]}, Points[1]={_newItem.Points[1]}");

                    // 提交
                    _canvas.RebuildSpatialIndex();
                }

                // 最终刷新
                _canvas.RedrawEditorLayer();
                _newItem = null;
            }
        }
        #endregion
    }
}
