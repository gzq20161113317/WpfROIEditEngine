using System;

namespace RoiEditor.Core.Undo
{
    /// <summary>
    /// 命令基类，提供默认实现
    /// </summary>
    public abstract class UndoableCommandBase : IUndoableCommand
    {
        public abstract string Description { get; }

        public abstract void Execute();

        public abstract void Undo();

        /// <summary>
        /// 默认的 Redo 实现：重新执行
        /// </summary>
        public virtual void Redo()
        {
            Execute();
        }

        /// <summary>
        /// 默认不支持合并
        /// </summary>
        public virtual bool CanMerge(IUndoableCommand other)
        {
            return false;
        }

        /// <summary>
        /// 默认合并实现（什么都不做）
        /// </summary>
        public virtual void Merge(IUndoableCommand other)
        {
            // 子类可以重写此方法实现合并逻辑
        }
    }
}
