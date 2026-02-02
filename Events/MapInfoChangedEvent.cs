using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace RoiEditor.Events
{
    public class MapInfoChangedEvent
    {
        public Rect MapBounds { get; }

        public MapInfoChangedEvent(Rect mapBounds)
        {
            MapBounds = mapBounds;
        }
    }
}
