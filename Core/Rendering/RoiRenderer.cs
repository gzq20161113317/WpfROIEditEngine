using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using RoiEditor.Models;

namespace RoiEditor.Core.Rendering
{
    /// <summary>
    /// RoiRenderer (画家组件)
    /// 职责：负责将 ROI 数据转换为视觉指令 (DrawingVisual)。
    /// 包含了所有的样式定义 (颜色、线宽、虚线模式)。
    /// </summary>
    public class RoiRenderer
    {
        // 样式常量 (以后改样式只改这里)
        private const double BASE_THICKNESS = 2.0;
        private const double HOVER_THICKNESS = 3.0;
        private const double FILL_OPACITY = 0.2;

        //手柄样式(Screen Space)
        private double HANDLE_SIZE = 8.0;//手柄大小8x8像素
        private readonly SolidColorBrush _handleFill = Brushes.White;
        private readonly Pen _handlePen = new Pen(Brushes.Black,1.0);

        public RoiRenderer()
        {
            if(_handlePen.CanFreeze)
                _handlePen.Freeze();
        }

        /// <summary>
        /// 绘制静态层 (所有未选中的 ROI)
        /// </summary>
        public void DrawStaticLayer(DrawingContext dc, IEnumerable<RoiItem> items, RoiItem hoverItem, double currentZoom)
        {
            if (items == null) return;

            // 根据缩放比动态计算线宽，保证视觉粗细一致
            double zoom = currentZoom <= 0 ? 1 : currentZoom;
            double thickness = BASE_THICKNESS / zoom;
            double hoverThick = HOVER_THICKNESS / zoom;

            // 预冻结画笔，提升性能
            // (注意：为了极致性能，真实项目中可以将常用颜色的 Pen 缓存起来，而不是每次 new)

            foreach (var item in items)
            {
                if (item == null || item.Points == null || item.Points.Count < 3) continue;
                if (item.IsSelected) continue; // 选中的由 EditorLayer 画

                var geom = BuildPolygonGeometry(item.Points);

                // 1. 准备画笔和填充
                var brush = new SolidColorBrush(item.Color) { Opacity = FILL_OPACITY };
                var pen = new Pen(new SolidColorBrush(item.Color), thickness);

                // 冻结对象 (WPF 性能关键)
                if (brush.CanFreeze) brush.Freeze();
                if (pen.Brush.CanFreeze) pen.Brush.Freeze();
                if (pen.CanFreeze) pen.Freeze();

                // 2. 绘制普通状态
                dc.DrawGeometry(brush, pen, geom);

                // 3. 绘制 Hover 高亮状态 (叠加一层)
                if (ReferenceEquals(item, hoverItem))
                {
                    var hoverPen = new Pen(new SolidColorBrush(item.Color), hoverThick)
                    {
                        // 圆头让高亮更好看
                        StartLineCap = PenLineCap.Round,
                        EndLineCap = PenLineCap.Round,
                        LineJoin = PenLineJoin.Round
                    };
                    if (hoverPen.Brush.CanFreeze) hoverPen.Brush.Freeze();
                    if (hoverPen.CanFreeze) hoverPen.Freeze();

                    dc.DrawGeometry(null, hoverPen, geom);
                }
            }
        }

        /// <summary>
        /// 绘制编辑层:虚线框 + 8点手柄
        /// </summary>
        public void DrawEditorLayer(DrawingContext dc, RoiItem activeItem, Matrix matrix)
        {
            if (activeItem == null || activeItem.Points == null || activeItem.Points.Count < 3) return;

            // 注意：编辑层通常是在 Screen Space (屏幕坐标系) 绘制，
            // 所以我们需要把 World 点转为 Screen 点
            var screenPts = activeItem.Points.Select(p => matrix.Transform(p)).ToList();
            var geom = BuildPolygonGeometry(screenPts);

            // 选中态样式：虚线、青色边框
            var fill = new SolidColorBrush(activeItem.Color) { Opacity = 0.25 };
            var pen = new Pen(Brushes.Cyan, 1.0) // 屏幕空间下，线宽固定为 2 即可
            {
                DashStyle = new DashStyle(new double[] { 4, 4 }, 0), // 虚线
            };

            if (fill.CanFreeze) fill.Freeze();
            if (pen.CanFreeze) pen.Freeze();

            dc.DrawGeometry(fill, pen, geom);

            if (!activeItem.IsEditing)
                return;

            //3.绘制8点控制手柄
            //获取屏幕空间的包围盒
            Rect bounds = geom.Bounds;

            double minDimension = Math.Min(bounds.Width, bounds.Height);
            double dynamicSize = Math.Min(HANDLE_SIZE, minDimension / 6.0);

            // 为了美观，限制最小显示尺寸，如果太小干脆就不画了（比如小于 2 像素）
            if (dynamicSize < 2.0) return;

            double halfSize = dynamicSize / 2;

            // 重新定义画笔，确保线宽适应小手柄 (如果手柄很小，线宽改细一点)
            Pen handlePen = _handlePen;
            if (dynamicSize < 4.0)
            {
                handlePen = new Pen(Brushes.Black, 0.5); // 极小手柄用细线
                handlePen.Freeze();
            }

            //计算8点位置
            Point[] handles = new Point[8];
            handles[0] = bounds.TopLeft;//TopLeft
            handles[1] = new Point(bounds.X + bounds.Width/2,bounds.Top);// Top
            handles[2] = bounds.TopRight;
            handles[3] = new Point(bounds.Right,bounds.Y + bounds.Height / 2);//Right
            handles[4] = bounds.BottomRight;
            handles[5] = new Point(bounds.X + bounds.Width / 2,bounds.Bottom);//Bottom
            handles[6] = bounds.BottomLeft;
            handles[7] = new Point(bounds.Left, bounds.Y + bounds.Height / 2);  // Left

            foreach (var p in handles)
            {
                // 手柄是正方形，居中绘制
                Rect r = new Rect(p.X - halfSize, p.Y - halfSize, HANDLE_SIZE, HANDLE_SIZE);
                dc.DrawRectangle(_handleFill, _handlePen, r);
            }
        }

        // 辅助方法：构建几何图形
        public static StreamGeometry BuildPolygonGeometry(IList<Point> pts)
        {
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(pts[0], true, true);
                ctx.PolyLineTo(pts.Skip(1).ToList(), true, false);
            }
            g.Freeze();
            return g;
        }
    }
}