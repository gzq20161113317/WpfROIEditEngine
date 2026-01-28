using Caliburn.Micro;
using RoiEditor.Enums;
using RoiEditor.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoiEditor.ViewModels.Component
{
    public class ToolBarViewModel : Screen
    {
        private readonly IEventAggregator _eventAggregator;
        private ROIOperationMode _currentOperationMode = ROIOperationMode.ROI_OS_Pan;
        private ROIDrawMode _currentDrawMode = ROIDrawMode.ROI_DS_Union;

        public ToolBarViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }

        public ROIOperationMode CurrentOperationMode
        {
            get { return _currentOperationMode; } 
            set
            {
                if(_currentOperationMode == value) return;
                _currentOperationMode = value;
                NotifyOfPropertyChange(() => CurrentOperationMode);

                _eventAggregator.PublishOnUIThreadAsync(new ROIOperationModeChangedEvent(_currentOperationMode));
            }
        }

        public ROIDrawMode CurrentDrawMode
        {
            get { return _currentDrawMode; }
            set
            {
                if (_currentDrawMode == value)
                    return;
                _currentDrawMode = value;
                NotifyOfPropertyChange(() => CurrentDrawMode);
                _eventAggregator.PublishOnUIThreadAsync(new ROIDrawModeChangedEvent(_currentDrawMode));
            }
        }

        public void SetROIOperationPan() => CurrentOperationMode = ROIOperationMode.ROI_OS_Pan;
        public void SetROIOperationSelect() => CurrentOperationMode = ROIOperationMode.ROI_OS_Select;
        public void SetROIOperationDrawRect() => CurrentOperationMode = ROIOperationMode.ROI_OS_ROI_Shape_Rectangle;

        public void FitToCoarsest()
        {
            _eventAggregator.PublishOnUIThreadAsync(new FitToCoarsestRequestEvent());
        }

        public void SetROIDrawModeUnion() => CurrentDrawMode = ROIDrawMode.ROI_DS_Union;

        protected override void OnActivate()
        {
            base.OnActivate();
            _eventAggregator.Subscribe(this);
        }

        protected override void OnDeactivate(bool close)
        {
            base.OnDeactivate(close);
            _eventAggregator.Unsubscribe(this);
        }


        //...其他模式...
    }
}
