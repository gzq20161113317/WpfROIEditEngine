using System;

namespace RoiEditor.Core.Undo
{
    /// <summary>
    /// 可撤销命令接口
    /// </summary>
    public interface IUndoableCommand
    {
        /// <summary>
        /// 执行命令
        /// </summary>
        void Execute();

        /// <summary>
        /// 撤销命令
        /// </summary>
        void Undo();

        /// <summary>
        /// 重做命令（默认实现为再次执行）
        /// </summary>
        void Redo();

        /// <summary>
        /// 命令描述（用于显示在 UI 上）
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 命令是否可以合并（用于连续的小操作，如拖拽）
        /// </summary>
        bool CanMerge(IUndoableCommand other);

        /// <summary>
        /// 合并命令
        /// </summary>
        void Merge(IUndoableCommand other);
    }
}
