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
    public class SubToolItem
    {
        public string Name { get; set; }
        public RoiEditor.Enums.ROIOperationMode ToolType { get; set; }

        // 【关键】改用 string 存储，避免 Geometry 对象带来的任何潜在渲染问题
        public string IconData { get; set; }

        // 【关键】使用 Command 确保点击 100% 触发
        public ICommand ClickCommand { get; set; }
    }
}
