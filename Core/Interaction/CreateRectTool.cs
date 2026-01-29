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
    public class CreateRectTool : ToolBase
    {
        private ROIRegion _newItem;
        private Point _startPoint;

        public CreateRectTool(Controls.RoiEditorCanvas canvas) : base(canvas) { }

        // 每次激活工具时，给 Canvas 一个崭新的 UI，绝对安全
        public override UIElement GetCustomCursorView() => CreateCrossCursorUI();

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

        public override void OnMouseDown(MouseButtonEventArgs e)
        {
            _startPoint = GetWorldPosition(e);

            _newItem = new ROIRegion
            {
                Name = "New Region",
                Color = Colors.Lime,
                Type = ROIRegionType.Rectangle,
                Points = new List<Point> { _startPoint, _startPoint, _startPoint, _startPoint }
            };

            // 临时添加到 Canvas 显示，但还没加到 ItemsSource (或者先加进去)
            // 简单做法：直接加到 ItemsSource
            _canvas.ItemsSource?.Add(_newItem);

            //2.选中它(IsSelected = true)
            //此时因为IsEditing还是false，所以屏幕上只会显示青色虚线框，不会显示手柄
            //这符合预期：拖拽过程中不需要看手柄
            _canvas.SelectROIRegion(_newItem);//SelectROIRegion会

            _canvas.CaptureMouse();
        }

        public override void OnMouseMove(MouseEventArgs e)
        {
            if (_newItem == null) return;

            var wPos = GetWorldPosition(e);

            // 更新矩形四个点
            _newItem.Points[1] = new Point(wPos.X, _startPoint.Y);
            _newItem.Points[2] = new Point(wPos.X, wPos.Y);
            _newItem.Points[3] = new Point(_startPoint.X, wPos.Y);

            //强制重绘编辑层（因为现在它是Selected，所以由EditorLayer负责绘制）
            _canvas.RedrawEditorLayer();
        }

        public override void OnMouseUp(MouseButtonEventArgs e)
        {
            if (_newItem != null)
            {
                // 规范化矩形 (处理负宽高)
                NormalizeRectPoints(_newItem.Points);

                Rect bounds = GetBoundingRect(_newItem.Points);

                // 判断：宽 和 高 都小于 2
                if (bounds.Width < 2.0 && bounds.Height < 2.0)
                {
                    // 视为无效绘制：从画布中移除刚才 MouseDown 创建的临时对象
                    _canvas.ItemsSource?.Remove(_newItem);
                    // 清理状态
                    _canvas.SelectROIRegion(null);
                    _newItem = null;
                    _canvas.ReleaseMouseCapture();
                    return; // 直接返回，不执行后面的选中或提交逻辑
                }

                _newItem.IsEditing = true;

                // 新增物体，重建索引
                _canvas.RebuildSpatialIndex();

                // 4. 画完一个后，自动切回“选择工具”
                // 除非设计是“连续画框模式”，否则切回 Select 体验更好
                //_canvas.Mode = DrawMode.Select;

                // 5. 刷新视图 (确保手柄显示出来)
                _canvas.RedrawEditorLayer();

                _newItem = null;
                _canvas.ReleaseMouseCapture();

                // 自动切回选择模式？(很多软件画完一个会自动切回，看你需求)
                // _canvas.Mode = DrawMode.Select; 
            }
        }

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
    }
}