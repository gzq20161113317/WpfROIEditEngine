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
        public const double HANDLE_SIZE = 6.0;//手柄大小6x6像素
        private const double MIN_ROI_SIZE_FOR_HANDLE = 15.0;//最小显示阈值

        private readonly SolidColorBrush _handleFill = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)); // 200/255 透明度
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

                var geom = BuildGeometry(item);

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
            // 1. 基础检查
            if (activeItem == null || activeItem.Points == null || activeItem.Points.Count < 2) return;

            // 2. [关键修复] 复用通用的 BuildGeometry 方法
            // 这样无论是圆、多边形还是贝塞尔，都能生成正确的形状
            var worldGeom = BuildGeometry(activeItem);
            if (worldGeom == null) return;

            // 3. [关键修复] 使用矩阵变换将形状从 World Space -> Screen Space
            // 这一步能保证圆形缩放后变成椭圆，多边形正确变形，且不需要手动算点
            var transform = new MatrixTransform(matrix);
            var screenGeom = worldGeom.GetFlattenedPathGeometry(); // 稍微平滑一下
            screenGeom.Transform = transform; // 应用变换

            // 4. 绘制选中框 (样式：半透明底 + 青色虚线边)
            var fill = new SolidColorBrush(activeItem.Color) { Opacity = 0.25 };
            var pen = new Pen(Brushes.Cyan, 1.0)
            {
                DashStyle = new DashStyle(new double[] { 4, 4 }, 0)
            };

            if (fill.CanFreeze) fill.Freeze();
            if (pen.CanFreeze) pen.Freeze();

            dc.DrawGeometry(fill, pen, screenGeom);

            // ==========================================================
            // 绘制手柄 (逻辑不变，但数据源变成了变换后的 screenGeom)
            // ==========================================================
            if (!activeItem.IsEditing) return;

            // 获取变换后的包围盒 (屏幕坐标)
            Rect bounds = screenGeom.GetRenderBounds(new Pen(Brushes.Black, 0.0));

            // 调用之前提取的静态方法获取手柄位置
            var handleRects = GetHandleRects(bounds);

            if (handleRects == null) return;

            // 确定手柄粗细
            double handleSize = handleRects[0].Width;
            Pen currentHandlePen = _handlePen;
            if (handleSize < 4.0)
            {
                currentHandlePen = new Pen(Brushes.Black, 0.5);
                currentHandlePen.Freeze();
            }

            foreach (var r in handleRects)
            {
                dc.DrawRectangle(_handleFill, currentHandlePen, r);
            }
        }


        public static double CalculateHandleSize(Rect bounds)
        {
            if (bounds.Width < MIN_ROI_SIZE_FOR_HANDLE || bounds.Height < MIN_ROI_SIZE_FOR_HANDLE)
                return 0;

            double minDimension = Math.Min(bounds.Width, bounds.Height);

            // 2. 修改动态比例
            // 原来是 / 3.0，改为 / 4.0 或 / 5.0
            // 意味着：物体必须更大，手柄才能达到最大值。物体较小时，手柄会显得更克制。
            double dynamicSize = Math.Min(HANDLE_SIZE, minDimension / 4.0);

            // 最小显示尺寸也可以稍微降一点点
            return dynamicSize < 2.0 ? 0 : dynamicSize;
        }

        public static List<Rect> GetHandleRects(Rect bounds)
        {
            double size = CalculateHandleSize(bounds);

            if (size <= 0) return null;

            double half = size / 2.0;

            // ... 生成 8 个点 ...
            Point[] centers = new Point[8];
            centers[0] = bounds.TopLeft;
            centers[1] = new Point(bounds.X + bounds.Width / 2, bounds.Top);
            centers[2] = bounds.TopRight;
            centers[3] = new Point(bounds.Right, bounds.Y + bounds.Height / 2);
            centers[4] = bounds.BottomRight;
            centers[5] = new Point(bounds.X + bounds.Width / 2, bounds.Bottom);
            centers[6] = bounds.BottomLeft;
            centers[7] = new Point(bounds.Left, bounds.Y + bounds.Height / 2);

            var rects = new List<Rect>(8);
            foreach (var p in centers)
            {
                rects.Add(new Rect(p.X - half, p.Y - half, size, size));
            }
            return rects;
        }



        public static StreamGeometry BuildGeometry(RoiItem item)
        {
            var pts = item?.Points;
            if (pts == null || pts.Count < 2) return null;

            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                switch (item.Type)
                {
                    case Enums.RoiType.Rectangle:
                    case Enums.RoiType.Polygon:
                        {
                            if (pts.Count < 3) break;

                            ctx.BeginFigure(pts[0], isFilled: true, isClosed: true);
                            ctx.PolyLineTo(pts.Skip(1).ToList(), isStroked: true, isSmoothJoin: false);
                            break;
                        }

                    case Enums.RoiType.Circle:
                        {
                            if (pts.Count < 2) break;

                            double rx = Math.Abs(pts[1].X - pts[0].X) / 2.0;
                            double ry = Math.Abs(pts[1].Y - pts[0].Y) / 2.0;
                            if (rx < 1e-6 || ry < 1e-6) break;

                            Point center = new Point((pts[0].X + pts[1].X) / 2.0, (pts[0].Y + pts[1].Y) / 2.0);

                            // 从右端点开始画两段半圆（ArcTo 两次闭合）
                            Point start = new Point(center.X + rx, center.Y);
                            Point left = new Point(center.X - rx, center.Y);

                            ctx.BeginFigure(start, isFilled: true, isClosed: true);
                            ctx.ArcTo(left, new Size(rx, ry), rotationAngle: 0,
                                      isLargeArc: false, sweepDirection: SweepDirection.Clockwise,
                                      isStroked: true, isSmoothJoin: false);
                            ctx.ArcTo(start, new Size(rx, ry), rotationAngle: 0,
                                      isLargeArc: false, sweepDirection: SweepDirection.Clockwise,
                                      isStroked: true, isSmoothJoin: false);
                            break;
                        }

                    case Enums.RoiType.Bezier:
                        {
                            if (pts.Count < 4) break;

                            ctx.BeginFigure(pts[0], isFilled: true, isClosed: false);
                            ctx.BezierTo(pts[1], pts[2], pts[3], isStroked: true, isSmoothJoin: false);
                            break;
                        }
                }
            }

            g.Freeze();
            return g;
        }

    }
}