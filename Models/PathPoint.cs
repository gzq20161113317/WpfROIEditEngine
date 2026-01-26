using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Serialization;

namespace RoiEditor.Models
{
    [Obsolete]
    public class PathPoint
    {
        public static PathPoint operator -(PathPoint A, PathPoint B) => new PathPoint(A.X - B.X,A.Y - B.Y);

        public static PathPoint operator +(PathPoint A, PathPoint B) => new PathPoint(A.X + B.X, A.Y + B.Y);

        public static bool operator ==(PathPoint A, PathPoint B)
        {
            if(Double.Equals(A.X,B.X) && Double.Equals(A.Y,B.Y))
            {
                return true;
            }
            return false;
        }

        public static bool operator !=(PathPoint A, PathPoint B)
        {
            if (Double.Equals(A.X, B.X) && Double.Equals(A.Y, B.Y))
            {
                return false;
            }
            return true;
        }

        public double X = 0;
        public double Y = 0;

        public PathPoint()
        {
            
        }

        public PathPoint(double xPos,double yPos)
        {
            X = xPos;
            Y = yPos;
        }

        public PathPoint(Point pt)
        {
            X = pt.X;
            Y = pt.Y;
        }
    }
}
