using System.Windows;
using System.Windows.Input;
using RoiEditor.Models;

namespace RoiEditor.Core.Interaction
{
    public class SelectTool : ToolBase
    {
        private bool _isDragging;
        private bool _isMouseDown;
        private Point _lastMouseWorld;
        private Point _dragStartScreen;
        private const double DRAG_THRESHOLD = 1.0;

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

            // 调用 Canvas 暴露出来的命中测试方法
            var hit = _canvas.HitTestRoi(wPos);

            if (hit != null)
            {
                // 选中逻辑
               if (_canvas.SelectedRoi != hit)
                {
                    _canvas.SelectRoi(hit);
                }

                bool isAltPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
                bool isDoubleClick = e.ClickCount == 2;

                if(isDoubleClick || isAltPressed)
                {
                    hit.IsEditing = true;
                    _canvas.RedrawEditorLayer();
                }

                _canvas.CaptureMouse();
            }
            else
            {
                _canvas.SelectRoi(null); // 取消选中
            }
        }

        public override void OnMouseMove(MouseEventArgs e)
        {
            var wPos = GetWorldPosition(e);

            if (_isMouseDown && _canvas.SelectedRoi != null)
            {

                // 如果还没进入拖拽状态，先检查距离
                if (!_isDragging)
                {
                    var curScreen = e.GetPosition(_canvas);
                    if ((curScreen - _dragStartScreen).Length > DRAG_THRESHOLD)
                    {
                        _isDragging = true; // 超过阈值，正式确认为拖拽
                    }
                }

                // 如果确认为拖拽，则执行移动逻辑
                if (_isDragging)
                {
                    var delta = wPos - _lastMouseWorld;
                    var roi = _canvas.SelectedRoi;
                    for (int i = 0; i < roi.Points.Count; i++)
                    {
                        roi.Points[i] += delta;
                    }
                    _canvas.RedrawEditorLayer();
                }
            }
            else
            {
                // Hover 高亮逻辑
                var hit = _canvas.HitTestRoi(wPos);
                if (hit != _canvas.SelectedRoi) // 不要高亮已经选中的
                {
                    _canvas.SetHoverRoi(hit);
                }
            }

            _lastMouseWorld = wPos;
        }

        public override void OnMouseUp(MouseButtonEventArgs e)
        {
            _isMouseDown = false;

            if (_isDragging)
            {
                _isDragging = false;
                _canvas.ReleaseMouseCapture();
                _canvas.RebuildSpatialIndex(); // 拖拽结束重建索引
            }
            else
            {
                // 是点击事件（未发生拖拽）：确保释放捕获
                if (_canvas.IsMouseCaptured)
                    _canvas.ReleaseMouseCapture();
            }
        }
    }
}