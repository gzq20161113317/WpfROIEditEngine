using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoiEditor.Enums
{
    public enum ROIRegionType
    {
        Rectangle, //矩形（4点：TL,TR,BR,BL）
        Polygon,//多边形（N点）
        Ellipse,//圆/椭圆（2点：PO=左上，P1=右下，定义包围盒）
        Bezier//贝塞尔(4点：Start,Control1,Control2,End)
    }
}
