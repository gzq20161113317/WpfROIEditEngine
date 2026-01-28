using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoiEditor.Enums
{
    public enum ROIOperationMode
    {
        ROI_OS_AutoRegion = 0,
        ROI_OS_FindRegion,
        ROI_OS_AutoRegion_Select,
        ROI_OS_AutoRegion_Select_Polygon,
        ROI_OS_FindRegion_OutLineSelect,
        ROI_OS_FindRegion_OutLineSelect_Polygon,
        ROI_OS_FindRegion_OutLinePattern,
        ROI_OS_FindRegion_Select,
        ROI_OS_FindRegion_Select_Polygon,
        ROI_OS_FindRegion_Find, 

        ROI_OS_ROI_Pen,
        ROI_OS_ROI_Pen_Curve,
        ROI_OS_ROI_Bend,
        ROI_OS_ROI_UnBend,
        ROI_OS_ROI_Redraw,
        ROI_OS_ROI_Smooth,
        ROI_OS_ROI_Shape_Rectangle,
        ROI_OS_ROI_Shape_Ellipse,
        ROI_OS_ROI_Shape_TwoPoint,

        ROI_OS_LineDraw,
        ROI_OS_Draw_Select,
        ROI_OS_MoveDrag,
        ROI_OS_MoveShape,
        ROI_OS_MultiMove,
        
        ROI_OS_Zoom_In,
        ROI_OS_Zoom_Out,

        ROI_OS_Delete_Range,

        ROI_OS_Pan,
        ROI_OS_Select,
        ROI_OS_Copy
    }
}
