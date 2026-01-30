using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoiEditor.Core.Helpers
{
    /// <summary>
    /// 通用的作用域守卫，用于自动设置和还原标志位
    /// </summary>
    public class ScopeGuard:IDisposable
    {
        private readonly Action _onDispose;

        public ScopeGuard(Action onEnter, Action onDispose)
        {
            onEnter?.Invoke();
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            _onDispose?.Invoke();
        }
    }
}
