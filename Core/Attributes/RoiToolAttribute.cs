using RoiEditor.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoiEditor.Core.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class RoiToolAttribute : Attribute
    {
        public ROIOperationMode Mode { get; }

        public RoiToolAttribute(ROIOperationMode mode)
        {
            Mode = mode;
        }
    }
}
