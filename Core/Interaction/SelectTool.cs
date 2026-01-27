using RoiEditor.Core.Rendering;
using RoiEditor.Models;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace RoiEditor.Core.Interaction
{
    public enum DragType
    {
        None, Body, TopLeft, Top, TopRight, Right, BottomRight, Bottom, BottomLeft, Left
    }

    public class SelectTool : ToolBase
    {
        private DragType _dragMode = DragType.None;
        private bool _isMouseDown;
        private Point _lastMouseWorld;
        private Point _dragStartScreen;
        private const double DRAG_THRESHOLD = 1.0;

        //手柄命中容差(像素)
        private const double HANDLE_TOLERANCE = 2.0;

        public SelectTool(Controls.RoiEditorCanvas canvas) : base(canvas) { }

        public override void OnActivated()
        {
            _canvas.Cursor = Cursors.Arrow;
        }

        public override void OnMouseDown(MouseButtonEventArgs e)
        {
            _isMouseDown = true;
            _dragStartScreen = e.GetPosition(_canvas);

            var wPos = GetWorldPosition(e);
            _lastMouseWorld = wPos;

            //1.先判断是否点中了手柄(前提：已有Region选且处于编辑模式)
            if (_canvas.SelectedRoi != null && _canvas.SelectedRoi.IsEditing)
            {
                var handle = GetHandleUnderMouse(e.GetPosition(_canvas), _canvas.SelectedRoi);
                if(handle != DragType.None)
                {
                    _dragMode = handle;
                    _canvas.CaptureMouse();
                    return; // 命中手柄，直接进入拉伸流程，不再做 HitTestRoi
                }
            }

            // 2. 如果没点中手柄，才进行常规的 ROI 命中测试
            var hit = _canvas.HitTestRoi(wPos);
            if (hit != null)
            {
                if (_canvas.SelectedRoi != hit)
                {
                    _canvas.SelectRoi(hit);
                    // 刚选中时不默认进入编辑模式，保持清爽
                }

                // 双击或Alt进入编辑模式
                bool isAltPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
                bool isDoubleClick = e.ClickCount == 2;
                if (isDoubleClick || isAltPressed)
                {
                    hit.IsEditing = true;
                    _canvas.RedrawEditorLayer();
                }

                // 准备移动整体
                _dragMode = DragType.Body;
                _canvas.CaptureMouse();
            }
            else
            {
                // 点空了：取消选中
                _canvas.SelectRoi(null);
                _dragMode = DragType.None;
            }
        }


        public override void OnMouseMove(MouseEventArgs e)
        {
            var wPos = GetWorldPosition(e);
            var sPos = e.GetPosition(_canvas);

            // 1. 拖拽逻辑
            if (_isMouseDown && _dragMode != DragType.None && _canvas.SelectedRoi != null)
            {
                // 防抖判断 (仅针对 Body 移动，拉伸通常不需要防抖，因为手柄很小)
                bool isDragging = true;
                if (_dragMode == DragType.Body)
                {
                    if ((sPos - _dragStartScreen).Length < DRAG_THRESHOLD)
                        isDragging = false;
                }

                if (isDragging)
                {
                    var roi = _canvas.SelectedRoi;

                    if (_dragMode == DragType.Body)
                    {
                        // 平移
                        var delta = wPos - _lastMouseWorld;
                        if(roi.Points != null)
                        {
                            for (int i = 0; i < roi.Points.Count; i++)
                                roi.Points[i] += delta;
                        }
                    }
                    else
                    {
                        // 拉伸
                        ResizeRoi(roi, _dragMode, wPos);
                    }

                    _canvas.RedrawEditorLayer();
                    _lastMouseWorld = wPos; // 更新坐标，防止累积误差
                }
            }
            else
            {
                // 2. Hover 逻辑：更新光标样式
                UpdateCursor(sPos);
            }
        }

        public override void OnMouseUp(MouseButtonEventArgs e)
        {
            _isMouseDown = false;

            if (_dragMode != DragType.None)
            {
                _canvas.ReleaseMouseCapture();

                // 拖拽/拉伸结束后，规范化矩形并重建索引
                if (_canvas.SelectedRoi != null)
                {
                    NormalizeByType(_canvas.SelectedRoi); 
                    _canvas.RebuildSpatialIndex();
                }

                _dragMode = DragType.None;
            }
        }

        // ==========================================================
        // 核心算法区
        // ==========================================================

        /// <summary>
        /// 通用变形算法：支持矩形、多边形、贝塞尔曲线、圆
        /// </summary>
        private void ResizeRoi(RoiItem item, DragType handle, Point currentWorldPos)
        {
            if (item == null || item.Points == null || item.Points.Count == 0)
                return;

            //1.Circle：2点定义包围盒，直接改包围盒最稳
            if (item.Type == Enums.RoiType.Circle)
            {
                ResizeCircleBBox(item,handle,currentWorldPos);
                return;
            }

            //计算【旧】包围盒（变形前的基准）
            //注意：这里必须重新计算一次原始包围盒
            //我们先算当前的Bounds,然后根据Handle和currentWorldPos算出目标Bounds
            Rect oldBounds = GetBoundingRect(item.Points);
            if (oldBounds.Width < 0.1 || oldBounds.Height < 0.1)
                return;

            Rect newBounds = GetResizedBounds(oldBounds,handle,currentWorldPos);

            if (Math.Abs(newBounds.Width) < 1e-6 || Math.Abs(newBounds.Height) < 1e-6)
                return;

            double scaleX = newBounds.Width / oldBounds.Width;
            double scaleY = newBounds.Height / oldBounds.Height;
            for(int i = 0;i < item.Points.Count;i++)
            {
                Point p = item.Points[i];

                double nx = (p.X - oldBounds.X);
                double ny = (p.Y - oldBounds.Y);

                double fx = newBounds.X + (nx * scaleX);
                double fy = newBounds.Y + (ny * scaleY);

                item.Points[i] = new Point(fx, fy);

            }
        }

        private void ResizeCircleBBox(RoiItem item,DragType handle,Point currentWorldPos)
        {
            if (item.Points.Count < 2)
                return;

            var tl = item.Points[0];
            var br = item.Points[1];

            double left = Math.Min(tl.X, br.X);
            double right = Math.Max(tl.X, br.X);
            double top = Math.Min(tl.Y, br.Y);
            double bottom = Math.Max(tl.Y, br.Y);

            Rect oldBounds = new Rect(left, top, right - left, bottom - top);
            if (oldBounds.Width < 0.1 || oldBounds.Height < 0.1) return;

            Rect newBounds = GetResizedBounds(oldBounds, handle, currentWorldPos);

            // 写回两点（TL/BR）
            item.Points[0] = newBounds.TopLeft;
            item.Points[1] = newBounds.BottomRight;
        }

        private Rect GetResizedBounds(Rect oldBounds,DragType handle,Point currentWorldPos)
        {
            double newLeft = oldBounds.Left;
            double newTop = oldBounds.Top;
            double newRight = oldBounds.Right;
            double newBottom = oldBounds.Bottom;

            switch (handle)
            {
                case DragType.TopLeft: newLeft = currentWorldPos.X; newTop = currentWorldPos.Y; break;
                case DragType.Top: newTop = currentWorldPos.Y; break;
                case DragType.TopRight: newRight = currentWorldPos.X; newTop = currentWorldPos.Y; break;
                case DragType.Right: newRight = currentWorldPos.X; break;
                case DragType.BottomRight: newRight = currentWorldPos.X; newBottom = currentWorldPos.Y; break;
                case DragType.Bottom: newBottom = currentWorldPos.Y; break;
                case DragType.BottomLeft: newLeft = currentWorldPos.X; newBottom = currentWorldPos.Y; break;
                case DragType.Left: newLeft = currentWorldPos.X; break;
            }

            // 注意：允许翻转（宽高为负时 scaleX/scaleY 为负，点集会镜像）
            return new Rect(newLeft, newTop, newRight - newLeft, newBottom - newTop);
        }

        private void NormalizeByType(RoiItem item)
        {
            if (item == null || item.Points == null) return;

            switch (item.Type)
            {
                case Enums.RoiType.Rectangle:
                    NormalizeRectangle(item);
                    break;

                case Enums.RoiType.Circle:
                    NormalizeCircle(item);
                    break;

                // Polygon / Bezier：不改点序，避免破坏语义
                default:
                    break;
            }
        }

        private void NormalizeRectangle(RoiItem item)
        {
            if (item.Points == null || item.Points.Count < 4) return;

            var r = GetBoundingRect(item.Points);

            // 重新按顺序写入 TL, TR, BR, BL
            item.Points[0] = r.TopLeft;
            item.Points[1] = r.TopRight;
            item.Points[2] = r.BottomRight;
            item.Points[3] = r.BottomLeft;
        }

        private void NormalizeCircle(RoiItem item)
        {
            if (item.Points == null || item.Points.Count < 2) return;

            var p0 = item.Points[0];
            var p1 = item.Points[1];

            double left = Math.Min(p0.X, p1.X);
            double right = Math.Max(p0.X, p1.X);
            double top = Math.Min(p0.Y, p1.Y);
            double bottom = Math.Max(p0.Y, p1.Y);

            item.Points[0] = new Point(left, top);
            item.Points[1] = new Point(right, bottom);
        }

        /// <summary>
        /// 判断鼠标在哪个手柄上 (使用 Renderer 的统一算法)
        /// </summary>
        private DragType GetHandleUnderMouse(Point screenPos, RoiItem item)
        {
           if(item == null) return DragType.None;

            var geom = RoiRenderer.BuildGeometry(item);
            if(geom == null) return DragType.None;

            //World -> Screen
            var screenGeom = geom.Clone();
            screenGeom.Transform = new MatrixTransform(_canvas.MainMatrix.Matrix);

            Rect bounds = screenGeom.GetRenderBounds(new Pen(Brushes.Black, 0.0));

            var handleRects = RoiRenderer.GetHandleRects(bounds);
            if (handleRects == null) return DragType.None;

            double tol = HANDLE_TOLERANCE;

            if (HitRect(handleRects[0], screenPos, tol)) return DragType.TopLeft;
            if (HitRect(handleRects[1], screenPos, tol)) return DragType.Top;
            if (HitRect(handleRects[2], screenPos, tol)) return DragType.TopRight;
            if (HitRect(handleRects[3], screenPos, tol)) return DragType.Right;
            if (HitRect(handleRects[4], screenPos, tol)) return DragType.BottomRight;
            if (HitRect(handleRects[5], screenPos, tol)) return DragType.Bottom;
            if (HitRect(handleRects[6], screenPos, tol)) return DragType.BottomLeft;
            if (HitRect(handleRects[7], screenPos, tol)) return DragType.Left;

            return DragType.None;
        }

        private bool HitRect(Rect r, Point p, double tolerance)
        {
            // 将矩形向外膨胀 tolerance
            Rect expanded = new Rect(
                r.X - tolerance,
                r.Y - tolerance,
                r.Width + tolerance * 2,
                r.Height + tolerance * 2);

            return expanded.Contains(p);
        }

        private void UpdateCursor(Point screenPos)
        {
            if (_canvas.SelectedRoi != null && _canvas.SelectedRoi.IsEditing)
            {
                var handle = GetHandleUnderMouse(screenPos, _canvas.SelectedRoi);
                switch (handle)
                {
                    case DragType.TopLeft:
                    case DragType.BottomRight: _canvas.Cursor = Cursors.SizeNWSE; return;
                    case DragType.TopRight:
                    case DragType.BottomLeft: _canvas.Cursor = Cursors.SizeNESW; return;
                    case DragType.Top:
                    case DragType.Bottom: _canvas.Cursor = Cursors.SizeNS; return;
                    case DragType.Left:
                    case DragType.Right: _canvas.Cursor = Cursors.SizeWE; return;
                }
            }

            // 如果没在手柄上，检查是不是在物体内
            var wPos = GetWorldPosition(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseMoveEvent });
            // 这里因为要转坐标稍微麻烦点，实际使用中可以复用 HitTestRoi 的缓存
            // 简单处理：如果是 Arrow 就保持 Arrow，如果是 Hand 就不管
            if (_canvas.Cursor != Cursors.Arrow && _canvas.Cursor != Cursors.Cross)
                _canvas.Cursor = Cursors.Arrow;
        }

        private Rect GetBoundingRect(IList<Point> points)
        {
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (var p in points)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }
    }
}