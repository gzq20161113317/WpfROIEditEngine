using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace RoiEditor.Views
{
    /// <summary>
    /// RoiMainView.xaml 的交互逻辑
    /// </summary>
    public partial class RoiMainView : UserControl
    {
        public RoiMainView()
        {
            InitializeComponent();
            // 订阅生命周期事件
            this.Loaded += OnViewLoaded;
            this.Unloaded += OnViewUnloaded;
        }

        private async void OnViewLoaded(object sender, RoutedEventArgs e)
        {
            // 安全转换 DataContext
            if (DataContext is IActivate activator)
            {
                // 手动触发 Activate
                // CM 内部有 IsActive 检查，所以重复调用也是安全的（幂等）
                activator.Activate();
            }
        }

        private async void OnViewUnloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is IDeactivate deactivator)
            {
                // close: false 表示 "我只是不可见了，还没被销毁"（保留状态）
                // close: true  表示 "我要被销毁了"（清理全部资源）
                // 视你的具体业务决定，通常 UserControl Unloaded 对应 close: false
                deactivator.Deactivate(close: false);
            }
        }
    }
}
