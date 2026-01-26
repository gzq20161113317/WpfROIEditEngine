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
        /// 绘制编辑层 (当前选中的 ROI)
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
            var pen = new Pen(Brushes.Cyan, 2.0) // 屏幕空间下，线宽固定为 2 即可
            {
                DashStyle = new DashStyle(new double[] { 4, 4 }, 0), // 虚线
            };

            if (fill.CanFreeze) fill.Freeze();
            if (pen.CanFreeze) pen.Freeze();

            dc.DrawGeometry(fill, pen, geom);

            // 未来可以在这里画 8 个调节手柄 (Resize Handles)
            // DrawHandles(dc, screenPts);
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