using RoiEditor.Controls;
using RoiEditor.Core.Attributes;
using RoiEditor.Core.Rendering;
using RoiEditor.Enums;
using RoiEditor.Models;
using System;
using System.Collections.Generic;
using System.Threading;
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

    [RoiTool(ROIOperationMode.ROI_OS_Select)]
    public class SelectROIRegionTool : ToolBase
    {
        private DragType _dragMode = DragType.None;
        private bool _isMouseDown;
        private bool _hasMoved;

        private Point _lastMouseWorld;
        private Point _dragStartScreen;

        private const double DRAG_THRESHOLD = 2.0;
        private const double HANDLE_TOLERANCE = 4.0;

        public SelectROIRegionTool(RoiEditorCanvas canvas) : base(canvas) { }

        public override void OnMouseDown(MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            _isMouseDown = true;
            _hasMoved = false;
            _dragStartScreen = e.GetPosition(_canvas);
            _lastMouseWorld = GetWorldPosition(e);

            // 1. 优先检测手柄
            if (TryStartResize(e)) return;

            // 2. 检测物体
            var hit = HitTestROI(_lastMouseWorld);

            if (hit != null)
            {
                HandleSelectionStrategy(hit, e);

                if (hit.IsSelected)
                {
                    _dragMode = DragType.Body;
                    _canvas.CaptureMouse();
                }
            }
            else
            {
                // 3. 点空了
                if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                {
                    _canvas.ClearSelection();
                }
                _dragMode = DragType.None;
            }
        }

        public override void OnMouseMove(MouseEventArgs e)
        {
            var wPos = GetWorldPosition(e);
            var sPos = e.GetPosition(_canvas);

            if (_isMouseDown && _dragMode != DragType.None)
            {
                if (!_hasMoved && (sPos - _dragStartScreen).Length > DRAG_THRESHOLD)
                {
                    _hasMoved = true;
                }

                if (_hasMoved)
                {
                    var delta = wPos - _lastMouseWorld;

                    if (_dragMode == DragType.Body)
                    {
                        // === 多选移动约束逻辑 ===
                        // 1. 计算所有选中物体的总包围盒 (预测移动后的位置)
                        Rect totalBounds = Rect.Empty;
                        foreach (var r in _canvas.SelectedRegions)
                            totalBounds.Union(GetBoundingRect(r.Points));

                        if (!totalBounds.IsEmpty)
                        {
                            Rect newBounds = new Rect(totalBounds.X + delta.X, totalBounds.Y + delta.Y, totalBounds.Width, totalBounds.Height);

                            // 2. 算出被钳制后的新包围盒
                            Rect clampedBounds = _canvas.ClampRectToValidRegion(newBounds);

                            // 3. 反推实际允许移动的 delta
                            Vector realDelta = new Vector(clampedBounds.X - totalBounds.X, clampedBounds.Y - totalBounds.Y);

                            // 4. 应用修正后的 delta
                            MoveSelectedRegions(realDelta);

                            foreach (var r in _canvas.SelectedRegions)
                            {
                                r.NotifyOfPropertyChange("Points");
                            }

                            // 注意：这里更新 _lastMouseWorld 需要小心，为了平滑体验，
                            // 我们通常更新为 "加上了 realDelta 的旧位置"，而不是鼠标的真实位置
                            // 或者简单点，直接让 _lastMouseWorld = wPos，但在边界会有“滑手”的感觉
                            // 工业软件通常的做法：
                            _lastMouseWorld += realDelta;
                        }
                    }
                    else
                    {
                        // === 拉伸约束逻辑 ===
                        // 拉伸时，直接限制鼠标位置即可
                        Point clampedPos = _canvas.ClampToValidRegion(wPos);
                        ResizeROIRegion(_canvas.SelectedROIRegion, _dragMode, clampedPos);

                        // 手动通知：告诉 ViewModel 属性变了，该算面积了！
                        if (_canvas.SelectedROIRegion != null)
                        {
                            _canvas.SelectedROIRegion.NotifyOfPropertyChange("Points");
                        }

                        _lastMouseWorld = wPos; // 拉伸时可以直接更随鼠标
                    }
                    _canvas.RedrawEditorLayer();
                }
            }
            else
            {
                UpdateCursor(sPos);
            }
        }

        public override void OnMouseUp(MouseButtonEventArgs e)
        {
            _isMouseDown = false;

            if (_dragMode != DragType.None)
            {
                _canvas.ReleaseMouseCapture();

                if (!_hasMoved)
                {
                    // === 【Bug 1 修复：双击闪烁问题】 ===
                    // 场景：点击了一个物体，但没拖拽
                    if (_dragMode == DragType.Body && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                    {
                        var hit = HitTestROI(GetWorldPosition(e));
                        if (hit != null)
                        {
                            // 检查当前是否已经是“单选该物体”的状态
                            bool isAlreadySingleSelected = _canvas.SelectedRegions.Count == 1 && _canvas.SelectedRegions[0] == hit;

                            // 只有当“状态需要改变”时才执行 Select
                            // 如果已经是单选它了（比如刚才双击导致的 IsEditing=true），这里什么都不做，
                            // 从而保护了 IsEditing 状态不被 SelectROIRegion 内部的 ClearSelection 重置掉
                            if (!isAlreadySingleSelected)
                            {
                                _canvas.SelectROIRegion(hit, isMultiSelect: false);
                            }
                        }
                    }
                }
                else
                {
                    // === 【Bug 2 修复：移动后无法命中】 ===
                    // 只要发生了几何变换（无论是拉伸还是平移），都必须重建索引

                    if (_dragMode != DragType.Body && _canvas.SelectedROIRegion != null)
                    {
                        NormalizeByType(_canvas.SelectedROIRegion);
                    }

                    // 以前这里套了个 if (_dragMode != Body)，导致平移后没重建索引
                    // 现在改为无条件重建
                    _canvas.RebuildSpatialIndex();
                }

                _dragMode = DragType.None;
            }
        }

        // ==========================================================
        // 辅助方法 (保持不变)
        // ==========================================================

        private bool TryStartResize(MouseButtonEventArgs e)
        {
            if (_canvas.SelectedRegions.Count == 1 &&
                _canvas.SelectedROIRegion != null &&
                _canvas.SelectedROIRegion.IsEditing)
            {
                var handle = GetHandleUnderMouse(e.GetPosition(_canvas), _canvas.SelectedROIRegion);
                if (handle != DragType.None)
                {
                    _dragMode = handle;
                    _canvas.CaptureMouse();
                    return true;
                }
            }
            return false;
        }

        private ROIRegion HitTestROI(Point wPos)
        {
            return _canvas.HitTestROIRegion(wPos, r =>
            {
                if (_canvas.ActiveROI == null) return true;
                return r.Parent == _canvas.ActiveROI;
            });
        }

        private void HandleSelectionStrategy(ROIRegion hit, MouseButtonEventArgs e)
        {
            bool isShift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

            if (isShift)
            {
                if (hit.IsSelected)
                    _canvas.DeselectROIRegion(hit);
                else
                    _canvas.SelectROIRegion(hit, isMultiSelect: true);
            }
            else
            {
                if (!hit.IsSelected)
                {
                    _canvas.SelectROIRegion(hit, isMultiSelect: false);
                }
            }

            // 双击逻辑
            if (hit.IsSelected && _canvas.SelectedRegions.Count == 1)
            {
                bool isAlt = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
                if (e.ClickCount == 2 || isAlt)
                {
                    hit.IsEditing = true;
                    _canvas.RedrawEditorLayer();
                }
            }
        }

        private void MoveSelectedRegions(Vector delta)
        {
            foreach (var region in _canvas.SelectedRegions)
            {
                if (region.Points != null)
                {
                    for (int i = 0; i < region.Points.Count; i++)
                        region.Points[i] += delta;
                }
            }
        }

        // ==========================================================
        // 算法区 
        // ==========================================================

        private void ResizeROIRegion(ROIRegion item, DragType handle, Point currentWorldPos)
        {
            if (item == null || item.Points == null || item.Points.Count == 0) return;

            if (item.Type == Enums.ROIRegionType.Circle)
            {
                ResizeCircleBBox(item, handle, currentWorldPos);
                return;
            }

            Rect oldBounds = GetBoundingRect(item.Points);
            if (oldBounds.Width < 0.1 || oldBounds.Height < 0.1) return;

            Rect newBounds = GetResizedBounds(oldBounds, handle, currentWorldPos);
            if (Math.Abs(newBounds.Width) < 1e-6 || Math.Abs(newBounds.Height) < 1e-6) return;

            double scaleX = newBounds.Width / oldBounds.Width;
            double scaleY = newBounds.Height / oldBounds.Height;

            for (int i = 0; i < item.Points.Count; i++)
            {
                Point p = item.Points[i];
                double nx = p.X - oldBounds.X;
                double ny = p.Y - oldBounds.Y;
                item.Points[i] = new Point(newBounds.X + nx * scaleX, newBounds.Y + ny * scaleY);
            }
        }

        private void ResizeCircleBBox(ROIRegion item, DragType handle, Point currentWorldPos)
        {
            if (item.Points.Count < 2) return;
            var tl = item.Points[0];
            var br = item.Points[1];
            double left = Math.Min(tl.X, br.X);
            double right = Math.Max(tl.X, br.X);
            double top = Math.Min(tl.Y, br.Y);
            double bottom = Math.Max(tl.Y, br.Y);

            Rect oldBounds = new Rect(left, top, right - left, bottom - top);
            if (oldBounds.Width < 0.1 || oldBounds.Height < 0.1) return;

            Rect newBounds = GetResizedBounds(oldBounds, handle, currentWorldPos);
            item.Points[0] = newBounds.TopLeft;
            item.Points[1] = newBounds.BottomRight;
        }

        private Rect GetResizedBounds(Rect oldBounds, DragType handle, Point currentWorldPos)
        {
            double newLeft = oldBounds.Left;
            double newTop = oldBounds.Top;
            double newRight = oldBounds.Right;
            double newBottom = oldBounds.Bottom;

            // 定义一个最小尺寸，防止缩成 0 或者翻转
            // 0.1 个单位通常足够小了，既看不出缝隙，又能保证数学计算不报错
            double minSize = 5;

            switch (handle)
            {
                case DragType.TopLeft:
                    // 限制：Left 不能超过 Right，Top 不能超过 Bottom
                    newLeft = Math.Min(currentWorldPos.X, oldBounds.Right - minSize);
                    newTop = Math.Min(currentWorldPos.Y, oldBounds.Bottom - minSize);
                    break;

                case DragType.Top:
                    newTop = Math.Min(currentWorldPos.Y, oldBounds.Bottom - minSize);
                    break;

                case DragType.TopRight:
                    // 限制：Right 不能小于 Left
                    newRight = Math.Max(currentWorldPos.X, oldBounds.Left + minSize);
                    newTop = Math.Min(currentWorldPos.Y, oldBounds.Bottom - minSize);
                    break;

                case DragType.Right:
                    newRight = Math.Max(currentWorldPos.X, oldBounds.Left + minSize);
                    break;

                case DragType.BottomRight:
                    newRight = Math.Max(currentWorldPos.X, oldBounds.Left + minSize);
                    // 限制：Bottom 不能小于 Top
                    newBottom = Math.Max(currentWorldPos.Y, oldBounds.Top + minSize);
                    break;

                case DragType.Bottom:
                    newBottom = Math.Max(currentWorldPos.Y, oldBounds.Top + minSize);
                    break;

                case DragType.BottomLeft:
                    newLeft = Math.Min(currentWorldPos.X, oldBounds.Right - minSize);
                    newBottom = Math.Max(currentWorldPos.Y, oldBounds.Top + minSize);
                    break;

                case DragType.Left:
                    newLeft = Math.Min(currentWorldPos.X, oldBounds.Right - minSize);
                    break;
            }

            // 这样算出来的 Width 和 Height 永远是正数 (>= minSize)
            return new Rect(newLeft, newTop, newRight - newLeft, newBottom - newTop);
        }

        private void NormalizeByType(ROIRegion item)
        {
            if (item == null || item.Points == null) return;
            switch (item.Type)
            {
                case Enums.ROIRegionType.Rectangle: NormalizeRectangle(item); break;
                case Enums.ROIRegionType.Circle: NormalizeCircle(item); break;
            }
        }

        private void NormalizeRectangle(ROIRegion item)
        {
            if (item.Points == null || item.Points.Count < 4) return;
            var r = GetBoundingRect(item.Points);
            item.Points[0] = r.TopLeft;
            item.Points[1] = r.TopRight;
            item.Points[2] = r.BottomRight;
            item.Points[3] = r.BottomLeft;
        }

        private void NormalizeCircle(ROIRegion item)
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

        private DragType GetHandleUnderMouse(Point screenPos, ROIRegion item)
        {
            if (item == null) return DragType.None;
            var geom = RoiRenderer.BuildGeometry(item);
            if (geom == null) return DragType.None;

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
            Rect expanded = new Rect(r.X - tolerance, r.Y - tolerance, r.Width + tolerance * 2, r.Height + tolerance * 2);
            return expanded.Contains(p);
        }

        private void UpdateCursor(Point screenPos)
        {
            if (_canvas.SelectedROIRegion != null && _canvas.SelectedROIRegion.IsEditing)
            {
                var handle = GetHandleUnderMouse(screenPos, _canvas.SelectedROIRegion);
                if (handle != DragType.None)
                {
                    switch (handle)
                    {
                        case DragType.TopLeft: case DragType.BottomRight: _canvas.Cursor = Cursors.SizeNWSE; return;
                        case DragType.TopRight: case DragType.BottomLeft: _canvas.Cursor = Cursors.SizeNESW; return;
                        case DragType.Top: case DragType.Bottom: _canvas.Cursor = Cursors.SizeNS; return;
                        case DragType.Left: case DragType.Right: _canvas.Cursor = Cursors.SizeWE; return;
                    }
                }
            }

            if (_canvas.Cursor != Cursors.Arrow && _canvas.Cursor != Cursors.Cross)
                _canvas.Cursor = Cursors.Arrow;
        }

        private Rect GetBoundingRect(IList<Point> points)
        {
            if (points == null || points.Count == 0) return Rect.Empty;
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