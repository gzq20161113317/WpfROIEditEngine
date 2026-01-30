using Caliburn.Micro;
using RoiEditor.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace RoiEditor.Models
{
    public class SubToolItem : PropertyChangedBase
    {
        private bool _isEnabled = true;

        public string Name { get; set; }
        public ROIOperationMode ToolType { get; set; }

        // 图标数据
        public string IconData { get; set; }

        // 点击命令
        public ICommand ClickCommand { get; set; }

        // 控制子菜单项是否可用
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    NotifyOfPropertyChange(() => IsEnabled);
                }
            }
        }

    }
}
