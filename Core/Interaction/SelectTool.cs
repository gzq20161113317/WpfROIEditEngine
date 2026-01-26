using System.Windows;
using System.Windows.Input;
using RoiEditor.Models;

namespace RoiEditor.Core.Interaction
{
    public class SelectTool : ToolBase
    {
        private bool _isDragging;
        private Point _lastMouseWorld;

        public SelectTool(Controls.RoiEditorCanvas canvas) : base(canvas) { }

        public override void OnActivated()
        {
            _canvas.Cursor = Cursors.Arrow;
        }

        public override void OnMouseDown(MouseButtonEventArgs e)
        {
            var wPos = GetWorldPosition(e);
            _lastMouseWorld = wPos;

            // 调用 Canvas 暴露出来的命中测试方法
            var hit = _canvas.HitTestRoi(wPos);

            if (hit != null)
            {
                // 选中逻辑
                _canvas.SelectRoi(hit); // 这一步设置 SelectedRoi 并重绘
                _isDragging = true;
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

            if (_isDragging && _canvas.SelectedRoi != null)
            {
                var delta = wPos - _lastMouseWorld;

                // 移动 ROI 的所有点
                var roi = _canvas.SelectedRoi;
                for (int i = 0; i < roi.Points.Count; i++)
                {
                    roi.Points[i] += delta;
                }

                // 只重绘编辑层，性能高
                _canvas.RedrawEditorLayer();
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
            if (_isDragging)
            {
                _isDragging = false;
                _canvas.ReleaseMouseCapture();

                // 拖拽结束，重建索引 (P0 级优化)
                _canvas.RebuildSpatialIndex();
            }
        }
    }
}