using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoiEditor.Enums
{
    public enum RegionRelation
    {
        Region_R1InR2 = 0,
        Region_R2InR1,
        Region_Intersect,
        Region_Equal,
        Region_No
    }

    public enum RegionSourceType
    {
        RegionSourceType_Hand = 0,
        RegionSourceType_Auto, 
        RegionSourceType_Outline
    }
}
