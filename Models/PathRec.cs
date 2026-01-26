using RoiEditor.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoiEditor.Models
{
    [Obsolete]
    public class PathRec
    {
        public PathPoint LeftTop = new PathPoint();
        public PathPoint RightBottom = new PathPoint();

        public RegionRelation JudgeRectangleIntersect(PathRec DstRec)
        {
            RegionRelation isIntersect = RegionRelation.Region_No;
            try
            {
                //逻辑就是判断Region的关系时否是上面五类
            }
            catch(Exception ex) 
            { 
                string str = ex.Message;
            }
            return isIntersect;
        }

        public bool isPointRect(PathPoint pt)
        {
            if(pt.X >= LeftTop.X && pt.X <= RightBottom.X &&
               pt.Y >= LeftTop.Y && pt.Y <= RightBottom.Y)
            {
                return true;
            }
            return false;
        }

        public double getAbsluteValue(double douValue)
        {
            if(douValue >= 0)
            {
                return douValue;
            }
            else
            {
                return douValue * -1;
            }
        }
    }
}
