using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace RoiEditor.Core.Interaction
{
    public class PanTool : ToolBase
    {
        private bool _isPanning;
        private Point _lastMouseScreen;

        public PanTool(Controls.RoiEditorCanvas canvas) : base(canvas) { }

        public override Cursor SystemCursor => Cursors.Hand;

        public override void OnMouseDown(MouseButtonEventArgs e)
        {
            _isPanning = true;
            _lastMouseScreen = e.GetPosition(_canvas);
            _canvas.CaptureMouse();
        }

        public override void OnMouseMove(MouseEventArgs e)
        {
            if (!_isPanning) return;

            var currentScreen = e.GetPosition(_canvas);
            var delta = currentScreen - _lastMouseScreen;

            // 操作 Canvas 的矩阵
            // 注意：MainMatrix 需要设为 internal 或 public 才能在这里访问
            Matrix m = _canvas.MainMatrix.Matrix;
            m.Translate(delta.X, delta.Y);
            _canvas.MainMatrix.Matrix = m;

            // 通知 Canvas 刷新
            // 注意：需要在 Canvas 里公开一个 Refresh 方法
            _canvas.PanRefresh();

            _lastMouseScreen = currentScreen;
        }

        public override void OnMouseUp(MouseButtonEventArgs e)
        {
            _isPanning = false;
            _canvas.ReleaseMouseCapture();

            // 鼠标松开时补一次立即刷新，保证最后一帧瓦片到位
            _canvas.RefreshTilesImmediate();
        }
    }
}