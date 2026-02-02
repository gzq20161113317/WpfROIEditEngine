using Caliburn.Micro;
using RoiEditor.Enums;
using RoiEditor.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoiEditor.ViewModels.Component.Setting
{
    public class SettingViewModel:Conductor<IScreen>,IHandle<ROIOperationModeChangedEvent>
    {
        private readonly IEventAggregator _eventAggregator;

        // 缓存各个子 ViewModel
        private readonly SelectSettingViewModel _selectSettingVM;
        // private readonly DrawDetailViewModel _drawDetailVM; // 以后扩展

        public SettingViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _selectSettingVM = new SelectSettingViewModel(eventAggregator);

            // 默认显示 Select 详情或者空
            ActivateItem(null);
        }

        public void Handle(ROIOperationModeChangedEvent message)
        {
            switch (message.CurrentOperationMode)
            {
                case ROIOperationMode.ROI_OS_Select:
                case ROIOperationMode.ROI_OS_MoveDrag:
                case ROIOperationMode.ROI_OS_MultiMove:
                    ActivateItem(_selectSettingVM);
                    break;

                // 这里可以扩展其他模式
                // case ROIOperationMode.ROI_OS_ROI_Shape_Rectangle:
                //    ActivateItem(_drawDetailVM);
                //    break;

                default:
                    // 如果不需要详情，可以 ActivateItem(null) 或者显示一个 EmptyViewModel
                    ActivateItem(null); 
                    break;
            }
        }

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
    }
}
