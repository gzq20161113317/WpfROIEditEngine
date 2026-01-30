using Caliburn.Micro;
using RoiEditor.Models;
using System.Windows.Media;

namespace RoiEditor.ViewModels.Component
{
    public class AddROIViewModel : Screen
    {
        private string _roiName = "New ROI";
        private Color _selectedColor = Colors.Red;

        public string ROIName
        {
            get => _roiName;
            set { _roiName = value; NotifyOfPropertyChange(() => ROIName); }
        }

        public Color SelectedColor
        {
            get => _selectedColor;
            set { _selectedColor = value; NotifyOfPropertyChange(() => SelectedColor); }
        }

        public ROI ResultROI { get; private set; }

        public void Confirm()
        {
            if (string.IsNullOrWhiteSpace(ROIName))
            {
                ROIName = "Unnamed ROI";
            }

            ResultROI = new ROI { Name = ROIName, Color = SelectedColor };
            TryClose(true);
        }

        public void Cancel()
        {
            TryClose(false);
        }
    }
}