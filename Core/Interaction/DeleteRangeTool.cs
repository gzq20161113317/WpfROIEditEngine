using RoiEditor.Core.Attributes;
using RoiEditor.Core.Undo.Commands;
using RoiEditor.Enums;
using RoiEditor.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RoiEditor.Core.Interaction
{
    [RoiTool(ROIOperationMode.ROI_OS_Delete_Range)]
    public class DeleteRangeTool : ToolBase
    {
        #region Fields
        private Point _startPoint; // 鼠标按下的起点
        private Point _currentPoint; // 当前鼠标位置
        private bool _isSelecting; // 是否正在拖拽选择框
        private Rectangle _selectionRect; // 临时选择框（用于显示）
        #endregion

        public DeleteRangeTool(Controls.RoiEditorCanvas canvas) : base(canvas) { }

        #region Cursor & UI
        public override UIElement GetCustomCursorView() => CreateDeleteCursorUI();

        private Grid CreateDeleteCursorUI()
        {
            var grid = new Grid { Margin = new Thickness(-10, -10, 0, 0) };

            // 十字准星 + X 标记（表示删除）
            var pathDataStr = "M10,0 L10,20 M0,10 L20,10 M5,5 L15,15 M15,5 L5,15";
            var geometry = Geometry.Parse(pathDataStr);
            if (geometry.CanFreeze) geometry.Freeze();

            var path = new Path
            {
                Data = geometry,
                Stroke = Brushes.Red,
                StrokeThickness = 2,
                SnapsToDevicePixels = true,
                IsHitTestVisible = false
            };

            grid.Children.Add(path);
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
                MessageBox.Show("Please select an ROI first.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var wPos = GetWorldPosition(e);

            // 校验有效区
            if (!_canvas.ValidRegion.IsEmpty && !_canvas.ValidRegion.Contains(wPos))
                return;

            // 开始选择
            _startPoint = wPos;
            _currentPoint = wPos;
            _isSelecting = true;
            _canvas.CaptureMouse();

            // 创建临时选择框
            CreateSelectionRectangle();
        }

        public override void OnMouseMove(MouseEventArgs e)
        {
            if (!_isSelecting) return;

            var wPos = GetWorldPosition(e);
            wPos = _canvas.ClampToValidRegion(wPos);
            _currentPoint = wPos;

            // 更新选择框
            UpdateSelectionRectangle();
        }

        public override void OnMouseUp(MouseButtonEventArgs e)
        {
            if (!_isSelecting) return;

            _isSelecting = false;
            _canvas.ReleaseMouseCapture();

            // 计算选择框的边界
            Rect selectionBounds = GetSelectionBounds();

            // 如果选择框太小，忽略
            if (selectionBounds.Width < 5.0 || selectionBounds.Height < 5.0)
            {
                // 移除临时选择框
                RemoveSelectionRectangle();
                return;
            }

            // 查找所有在选择框内或相交的 Region
            var regionsToDelete = FindRegionsInBounds(selectionBounds);

            if (regionsToDelete.Count == 0)
            {
                // 移除临时选择框
                RemoveSelectionRectangle();
                MessageBox.Show("No regions found in the selection area.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 弹窗确认（选择框保持可见）
            var result = MessageBox.Show(
                $"Delete {regionsToDelete.Count} region(s) in the selection area?\nThis action can be undone with Ctrl+Z.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            // 弹窗关闭后，移除选择框
            RemoveSelectionRectangle();

            if (result == MessageBoxResult.Yes)
            {
                // 使用 Undo 命令删除
                DeleteRegions(regionsToDelete);
            }
        }
        #endregion

        #region Selection Rectangle Management
        private void CreateSelectionRectangle()
        {
            _selectionRect = new Rectangle
            {
                Stroke = Brushes.Red,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Fill = new SolidColorBrush(Color.FromArgb(30, 255, 0, 0)), // 半透明红色
                IsHitTestVisible = false
            };

            // 添加到 Canvas 的 Overlay 层
            _canvas.OverlayCanvas.Children.Add(_selectionRect);
        }

        private void UpdateSelectionRectangle()
        {
            if (_selectionRect == null) return;

            // 将世界坐标转换为屏幕坐标
            Point screenStart = _canvas.WorldToScreen(_startPoint);
            Point screenCurrent = _canvas.WorldToScreen(_currentPoint);

            double left = Math.Min(screenStart.X, screenCurrent.X);
            double top = Math.Min(screenStart.Y, screenCurrent.Y);
            double width = Math.Abs(screenCurrent.X - screenStart.X);
            double height = Math.Abs(screenCurrent.Y - screenStart.Y);

            Canvas.SetLeft(_selectionRect, left);
            Canvas.SetTop(_selectionRect, top);
            _selectionRect.Width = width;
            _selectionRect.Height = height;
        }

        private void RemoveSelectionRectangle()
        {
            if (_selectionRect != null)
            {
                _canvas.OverlayCanvas.Children.Remove(_selectionRect);
                _selectionRect = null;
            }
        }
        #endregion

        #region Helper Methods
        private Rect GetSelectionBounds()
        {
            double left = Math.Min(_startPoint.X, _currentPoint.X);
            double top = Math.Min(_startPoint.Y, _currentPoint.Y);
            double right = Math.Max(_startPoint.X, _currentPoint.X);
            double bottom = Math.Max(_startPoint.Y, _currentPoint.Y);

            return new Rect(new Point(left, top), new Point(right, bottom));
        }

        private List<ROIRegion> FindRegionsInBounds(Rect bounds)
        {
            var result = new List<ROIRegion>();

            if (_canvas.ActiveROI == null || _canvas.ActiveROI.Regions == null)
                return result;

            foreach (var region in _canvas.ActiveROI.Regions)
            {
                if (region.Points == null || region.Points.Count == 0)
                    continue;

                // 检查 Region 是否在选择框内或相交
                if (IsRegionIntersectingBounds(region, bounds))
                {
                    result.Add(region);
                }
            }

            return result;
        }

        private bool IsRegionIntersectingBounds(ROIRegion region, Rect bounds)
        {
            // 获取 Region 的边界框
            Rect regionBounds = GetRegionBounds(region);

            // 检查是否相交
            return bounds.IntersectsWith(regionBounds);
        }

        private Rect GetRegionBounds(ROIRegion region)
        {
            if (region.Points == null || region.Points.Count == 0)
                return Rect.Empty;

            double minX = region.Points.Min(p => p.X);
            double minY = region.Points.Min(p => p.Y);
            double maxX = region.Points.Max(p => p.X);
            double maxY = region.Points.Max(p => p.Y);

            return new Rect(new Point(minX, minY), new Point(maxX, maxY));
        }

        private void DeleteRegions(List<ROIRegion> regions)
        {
            if (regions.Count == 0) return;

            // 创建批量删除命令
            var command = new DeleteMultipleRegionsCommand(_canvas.ActiveROI, regions, _canvas.EventAggregator);
            _canvas.UndoManager?.ExecuteCommand(command);

            // 清空选中
            _canvas.SelectROIRegion(null);
        }
        #endregion

        protected override void OnDeactivated()
        {
            base.OnDeactivated();
            // 清理临时选择框
            RemoveSelectionRectangle();
        }
    }
}
