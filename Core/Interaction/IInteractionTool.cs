using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace RoiEditor.Core.Interaction
{
    /// <summary>
    /// 交互工具接口 (状态模式)
    /// </summary>
    public interface IInteractionTool
    {
        // 工具被激活时触发 (如切换光标)
        void OnActivated();

        // 工具被停用时触发 (如清理临时状态)
        void OnDeactivated();

        // 核心交互事件
        void OnMouseDown(MouseButtonEventArgs e);
        void OnMouseMove(MouseEventArgs e);
        void OnMouseUp(MouseButtonEventArgs e);

        // 如果需要支持键盘快捷键 (如 Delete 删除 ROI)
        void OnKeyDown(KeyEventArgs e);
    }
}
