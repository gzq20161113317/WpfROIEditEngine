using RoiEditor.Core.Attributes;
using RoiEditor.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace RoiEditor.Core.Interaction
{
    [RoiTool(ROIOperationMode.ROI_OS_ROI_Pen)]
    public class PenTool : ToolBase
    {
        public override Cursor SystemCursor => Cursors.Pen;

        public PenTool(Controls.RoiEditorCanvas canvas) : base(canvas) { }

        public override void OnMouseDown(MouseButtonEventArgs e)
        {
            
        }

        public override void OnMouseMove(MouseEventArgs e)
        {
          
        }

        public override void OnMouseUp(MouseButtonEventArgs e)
        {
            
        }
    }
}
