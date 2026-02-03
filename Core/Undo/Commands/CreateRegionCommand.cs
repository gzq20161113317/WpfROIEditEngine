using RoiEditor.Models;
using System.Collections.ObjectModel;

namespace RoiEditor.Core.Undo.Commands
{
    /// <summary>
    /// 创建 Region 命令
    /// </summary>
    public class CreateRegionCommand : UndoableCommandBase
    {
        private readonly ObservableCollection<ROIRegion> _regionCollection;
        private readonly ROIRegion _region;
        private readonly string _roiName;

        public override string Description => $"Create Region in {_roiName}";

        public CreateRegionCommand(ObservableCollection<ROIRegion> regionCollection, ROIRegion region, string roiName)
        {
            _regionCollection = regionCollection;
            _region = region;
            _roiName = roiName;
        }

        public override void Execute()
        {
            _regionCollection.Add(_region);
        }

        public override void Undo()
        {
            _regionCollection.Remove(_region);
        }
    }
}
