using RoiEditor.Core.Rendering;
using RoiEditor.Models;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

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
        private const double HANDLE_HIT_SIZE = 8.0;

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
                    var delta = wPos - _lastMouseWorld;

                    if (_dragMode == DragType.Body)
                    {
                        // 平移
                        var roi = _canvas.SelectedRoi;
                        for (int i = 0; i < roi.Points.Count; i++) roi.Points[i] += delta;
                    }
                    else
                    {
                        // 拉伸
                        ResizeRoi(_canvas.SelectedRoi, _dragMode, wPos);
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
                    NormalizeRoi(_canvas.SelectedRoi); // 防止拉伸出负宽高的矩形
                    _canvas.RebuildSpatialIndex();
                }

                _dragMode = DragType.None;
            }
        }

        // ==========================================================
        // 核心算法区
        // ==========================================================

        /// <summary>
        /// 根据鼠标位置，拉伸矩形
        /// 假设 ROI 是个标准矩形 (P0=TL, P1=TR, P2=BR, P3=BL)
        /// </summary>
        private void ResizeRoi(RoiItem item, DragType handle, Point currentWorldPos)
        {
            if (item.Points == null || item.Points.Count < 4) return;

            // 获取当前包围盒
            var rect = GetBoundingRect(item.Points);
            double left = rect.Left;
            double top = rect.Top;
            double right = rect.Right;
            double bottom = rect.Bottom;

            // 根据手柄修改边界
            // 注意：这里没有限制最小宽高，可能会拉反，依靠 OnMouseUp 的 Normalize 修复
            switch (handle)
            {
                case DragType.TopLeft: left = currentWorldPos.X; top = currentWorldPos.Y; break;
                case DragType.Top: top = currentWorldPos.Y; break;
                case DragType.TopRight: right = currentWorldPos.X; top = currentWorldPos.Y; break;
                case DragType.Right: right = currentWorldPos.X; break;
                case DragType.BottomRight: right = currentWorldPos.X; bottom = currentWorldPos.Y; break;
                case DragType.Bottom: bottom = currentWorldPos.Y; break;
                case DragType.BottomLeft: left = currentWorldPos.X; bottom = currentWorldPos.Y; break;
                case DragType.Left: left = currentWorldPos.X; break;
            }

            // 直接重写 4 个点
            item.Points[0] = new Point(left, top);
            item.Points[1] = new Point(right, top);
            item.Points[2] = new Point(right, bottom);
            item.Points[3] = new Point(left, bottom);
        }

        /// <summary>
        /// 判断鼠标在哪个手柄上 (使用 Renderer 的统一算法)
        /// </summary>
        private DragType GetHandleUnderMouse(Point screenPos, RoiItem item)
        {
            // 1. 先计算 ROI 在屏幕上的包围盒
            var m = _canvas.MainMatrix.Matrix;

            // 简单把四个角转到屏幕坐标求 Bounds
            // (这里假设是矩形，如果是任意多边形，逻辑也是求 Screen Bounds)
            var p0 = m.Transform(item.Points[0]);
            var p2 = m.Transform(item.Points[2]);

            double l = Math.Min(p0.X, p2.X);
            double t = Math.Min(p0.Y, p2.Y);
            double r = Math.Max(p0.X, p2.X);
            double b = Math.Max(p0.Y, p2.Y);

            Rect bounds = new Rect(l, t, r - l, b - t);

            // 2. 直接调用 Renderer 的静态方法获取 8 个手柄的准确位置
            var handleRects = RoiRenderer.GetHandleRects(bounds);

            // 如果返回 null，说明 Renderer 觉得太小没画，那自然也点不中
            if (handleRects == null) return DragType.None;

            // 3. 稍微扩大一点点击判定范围 (比如 +2px)，提升手感
            // 否则 8px 的手柄太难点了
            double tolerance = 2.0;

            // 顺序对应：TL, T, TR, R, BR, B, BL, L
            if (HitRect(handleRects[0], screenPos, tolerance)) return DragType.TopLeft;
            if (HitRect(handleRects[1], screenPos, tolerance)) return DragType.Top;
            if (HitRect(handleRects[2], screenPos, tolerance)) return DragType.TopRight;
            if (HitRect(handleRects[3], screenPos, tolerance)) return DragType.Right;
            if (HitRect(handleRects[4], screenPos, tolerance)) return DragType.BottomRight;
            if (HitRect(handleRects[5], screenPos, tolerance)) return DragType.Bottom;
            if (HitRect(handleRects[6], screenPos, tolerance)) return DragType.BottomLeft;
            if (HitRect(handleRects[7], screenPos, tolerance)) return DragType.Left;

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

        private void NormalizeRoi(RoiItem item)
        {
            var r = GetBoundingRect(item.Points);
            // 重新按顺序写入 TL, TR, BR, BL
            item.Points[0] = r.TopLeft;
            item.Points[1] = r.TopRight;
            item.Points[2] = r.BottomRight;
            item.Points[3] = r.BottomLeft;
        }
    }
}