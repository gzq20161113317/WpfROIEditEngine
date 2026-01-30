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
        // true: 干完活(Activate)就结束，不替换 _currentTool
        // false: (默认) 长效工具，需要替换 _currentTool 并接管鼠标
        bool IsActionOnly { get; }

        // 工具被激活时触发 (如切换光标)
        void Activate();

        // 工具被停用时触发 (如清理临时状态)
        void Deactivate();

        // 核心交互事件
        void OnMouseDown(MouseButtonEventArgs e);
        void OnMouseMove(MouseEventArgs e);
        void OnMouseUp(MouseButtonEventArgs e);

        // 如果需要支持键盘快捷键 (如 Delete 删除 ROI)
        void OnKeyDown(KeyEventArgs e);
    }
}
