using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace RoiEditor.Core.Rendering
{
    public class VisualHost:FrameworkElement
    {
        private readonly VisualCollection _children;
        public VisualHost() { _children = new VisualCollection(this); }

        public void SetVisual(Visual visual)
        {
            _children.Clear();
            if (visual != null) _children.Add(visual);
        }

        protected override int VisualChildrenCount => _children.Count;
        protected override Visual GetVisualChild(int index) => _children[index];
    }
}
