using RoiEditor.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoiEditor.Events
{
    public class ROIDrawModeChangedEvent
    {
        public ROIDrawMode CurrentDrawMode { get; set; }

        public ROIDrawModeChangedEvent(ROIDrawMode drawMode)
        {
            CurrentDrawMode = drawMode;
        }
    }
}
