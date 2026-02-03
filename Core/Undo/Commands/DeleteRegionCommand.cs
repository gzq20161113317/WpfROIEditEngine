using RoiEditor.Models;
using System.Collections.ObjectModel;

namespace RoiEditor.Core.Undo.Commands
{
    /// <summary>
    /// 删除 Region 命令
    /// </summary>
    public class DeleteRegionCommand : UndoableCommandBase
    {
        private readonly ObservableCollection<ROIRegion> _regionCollection;
        private readonly ROIRegion _region;
        private readonly int _index;
        private readonly string _roiName;

        public override string Description => $"Delete Region from {_roiName}";

        public DeleteRegionCommand(ObservableCollection<ROIRegion> regionCollection, ROIRegion region, string roiName)
        {
            _regionCollection = regionCollection;
            _region = region;
            _index = regionCollection.IndexOf(region);
            _roiName = roiName;
        }

        public override void Execute()
        {
            _regionCollection.Remove(_region);
        }

        public override void Undo()
        {
            if (_index >= 0 && _index <= _regionCollection.Count)
            {
                _regionCollection.Insert(_index, _region);
            }
            else
            {
                _regionCollection.Add(_region);
            }
        }
    }
}
