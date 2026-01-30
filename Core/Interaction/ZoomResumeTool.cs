using RoiEditor.Controls;
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
    [RoiTool(ROIOperationMode.ROI_OS_Zoom_Resume)]
    public class ZoomResumeTool : ToolBase
    {
        public ZoomResumeTool(RoiEditorCanvas canvas) : base(canvas) { }

        // 【关键】声明自己是瞬时的
        public override bool IsActionOnly => true;

        // 【关键】把原本写在 SwitchTool 里的逻辑搬到这里
        protected override void OnActivated()
        {
            _canvas.FitToCoarsestAndCenter();
        }

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
