using RoiEditor.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoiEditor.Events
{
    public class RegionSelectionChangedEvent
    {
        public List<ROIRegion> SelectedRegions { get; }

        public RegionSelectionChangedEvent(IEnumerable<ROIRegion> regions)
        {
            SelectedRegions = regions != null ? regions.ToList() : new List<ROIRegion>();
        }
    }
}
