using RoiEditor.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoiEditor.Events
{
    public class ROIOperationModeChangedEvent
    {

        public ROIOperationMode CurrentOperationMode { get; set; }

        public ROIOperationModeChangedEvent(ROIOperationMode operationMode)
        {
            CurrentOperationMode = operationMode;
        }
    }
}
