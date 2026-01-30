using RoiEditor.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoiEditor.Events
{
    public class ActiveROIChangedEvent
    {
        public ROI ActiveROI { get; }
        public ActiveROIChangedEvent(ROI roi) => ActiveROI = roi;
    }
}
