using Caliburn.Micro;
using RoiEditor.Models;
using RoiEditor.Enums;
using RoiEditor.ViewModels; // 确保引用了 RelayCommand

namespace RoiEditor.ViewModels.Component
{
    public class ToolItemViewModel : PropertyChangedBase
    {
        // ==========================================
        // 【新增】手动添加 Parent 属性
        // 之前继承 Screen 时是自带的，现在需要自己定义
        // ==========================================
        public object Parent { get; set; }

        // 直接存字符串，通知 UI 更新
        private string _iconData;
        public string IconData
        {
            get { return _iconData; }
            set { _iconData = value; NotifyOfPropertyChange(() => IconData); }
        }

        // 名字
        private string _name;
        public new string Name // 覆盖 Screen 的 DisplayName
        {
            get { return _name; }
            set { _name = value; NotifyOfPropertyChange(() => Name); }
        }

        public ROIOperationMode ToolType { get; set; }
        public bool IsActionOnly { get; set; }

        public bool HasSubTools => SubTools != null && SubTools.Count > 0;
        public BindableCollection<SubToolItem> SubTools { get; private set; }

        private bool _isActive;
        public bool IsActive
        {
            get { return _isActive; }
            set { _isActive = value; NotifyOfPropertyChange(() => IsActive); }
        }

        private bool _isPopupOpen;
        public bool IsPopupOpen
        {
            get { return _isPopupOpen; }
            set { _isPopupOpen = value; NotifyOfPropertyChange(() => IsPopupOpen); }
        }

        // 是否显示右侧分割线
        private bool _isSeparatorVisible;
        public bool IsSeparatorVisible
        {
            get { return _isSeparatorVisible; }
            set { _isSeparatorVisible = value; NotifyOfPropertyChange(() => IsSeparatorVisible); }
        }

        public System.Windows.Input.ICommand ClickCommand { get; set; }

        // 构造函数：全 String 入参
        public ToolItemViewModel(string name, ROIOperationMode toolType, string iconData, bool isActionOnly = false, SubToolItem[] subTools = null)
        {
            Name = name;
            ToolType = toolType;
            IconData = iconData; // 直接赋值字符串
            IsActionOnly = isActionOnly;

            ClickCommand = new RelayCommand(_ => Execute());

            if (subTools != null && subTools.Length > 0)
            {
                foreach (var sub in subTools)
                {
                    // 【绑定命令】
                    // 这里的逻辑会在点击子菜单时直接执行，无需 UI 查找
                    sub.ClickCommand = new RelayCommand(_ =>
                    {
                        // 1. 自身变身
                        Name = sub.Name;
                        IconData = sub.IconData; // 字符串替换，UI自动重绘
                        ToolType = sub.ToolType;

                        // 2. 关闭弹窗
                        IsPopupOpen = false;

                        // 3. 通知 Toolbar (父级)
                        var parent = this.Parent as ToolBarViewModel;
                        parent?.HandleToolSelection(this);
                    });
                }
                SubTools = new BindableCollection<SubToolItem>(subTools);
            }
        }

        private void Execute()
        {
            if (HasSubTools) IsPopupOpen = !IsPopupOpen;
            else
            {
                var parent = this.Parent as ToolBarViewModel;
                parent?.HandleToolSelection(this);
            }
        }
    }
}