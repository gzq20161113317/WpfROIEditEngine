using RoiEditor.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace RoiEditor.Core.Interaction
{
    public abstract class ToolBase : IInteractionTool
    {
        protected readonly RoiEditorCanvas _canvas;

        protected ToolBase(RoiEditorCanvas canvas)
        {
            _canvas = canvas;
        }

        // 默认行为：激活时关闭自定义光标，使用系统箭头
        // 选项 A：使用系统光标 (默认箭头)
        // 如果子类不重写 CustomCursorView，就用这个
        public virtual Cursor SystemCursor => Cursors.Arrow;

        // 选项 B：使用自定义 UI 光标
        // 默认返回 null (代表不使用自定义，用系统光标)
        public virtual UIElement GetCustomCursorView() => null;
        public void Activate()
        {
            // 步骤 A: 设置光标
            SetupCursor();

            // 步骤 B: 调用子类的 Protected 钩子
            OnActivated();
        }

        public void Deactivate()
        {
            // 步骤 A: 基类恢复光标
            _canvas.SetSystemCursor(Cursors.Arrow);

            // 步骤 B: 调用子类的 Protected 钩子
            OnDeactivated();
        }

        protected virtual void OnActivated() { }
        protected virtual void OnDeactivated() { }

        public virtual void OnKeyDown(KeyEventArgs e) { }

        // 强制子类实现鼠标事件
        public abstract void OnMouseDown(MouseButtonEventArgs e);
        public abstract void OnMouseMove(MouseEventArgs e);
        public abstract void OnMouseUp(MouseButtonEventArgs e);

        // 辅助：坐标转换
        protected Point GetWorldPosition(MouseEventArgs e)
        {
            var screenPos = e.GetPosition(_canvas);
            var m = _canvas.MainMatrix.Matrix;
            if (m.HasInverse) m.Invert();
            return m.Transform(screenPos);
        }

        private void SetupCursor()
        {
            var customView = GetCustomCursorView();
            if (customView != null)
                _canvas.SetCustomCursor(customView);
            else
                _canvas.SetSystemCursor(SystemCursor);
        }
    }
}
