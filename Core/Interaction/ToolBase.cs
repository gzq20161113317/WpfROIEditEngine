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

        public virtual void OnActivated() { }
        public virtual void OnDeactivated() { }
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
    }
}
