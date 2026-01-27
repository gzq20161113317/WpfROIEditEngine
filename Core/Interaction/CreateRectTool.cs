using RoiEditor.Enums;
using RoiEditor.Models;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace RoiEditor.Core.Interaction
{
    public class CreateRectTool : ToolBase
    {
        private ROIRegion _newItem;
        private Point _startPoint;

        public CreateRectTool(Controls.RoiEditorCanvas canvas) : base(canvas) { }

        public override void OnActivated()
        {
            _canvas.Cursor = Cursors.Cross;
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
            _canvas.SelectROIRegion(_newItem);

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
    }
}