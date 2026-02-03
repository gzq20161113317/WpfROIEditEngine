using System;
using System.Collections.Generic;
using System.Linq;

namespace RoiEditor.Core.Undo
{
    /// <summary>
    /// Undo/Redo 管理器
    /// 职责：管理命令栈，提供撤销/重做功能
    /// </summary>
    public class UndoManager
    {
        private readonly Stack<IUndoableCommand> _undoStack = new Stack<IUndoableCommand>();
        private readonly Stack<IUndoableCommand> _redoStack = new Stack<IUndoableCommand>();
        private readonly int _maxStackSize;

        /// <summary>
        /// 是否可以撤销
        /// </summary>
        public bool CanUndo => _undoStack.Count > 0;

        /// <summary>
        /// 是否可以重做
        /// </summary>
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// 下一个撤销操作的描述
        /// </summary>
        public string UndoDescription => CanUndo ? _undoStack.Peek().Description : null;

        /// <summary>
        /// 下一个重做操作的描述
        /// </summary>
        public string RedoDescription => CanRedo ? _redoStack.Peek().Description : null;

        /// <summary>
        /// 状态变化事件（用于更新 UI）
        /// </summary>
        public event EventHandler StateChanged;

        public UndoManager(int maxStackSize = 100)
        {
            _maxStackSize = maxStackSize;
        }

        /// <summary>
        /// 执行命令并记录到 Undo 栈
        /// </summary>
        public void ExecuteCommand(IUndoableCommand command)
        {
            if (command == null) return;

            // 执行命令
            command.Execute();

            // 尝试合并命令（用于连续的小操作，如拖拽）
            if (_undoStack.Count > 0)
            {
                var lastCommand = _undoStack.Peek();
                if (lastCommand.CanMerge(command))
                {
                    lastCommand.Merge(command);
                    OnStateChanged();
                    return;
                }
            }

            // 添加到 Undo 栈
            _undoStack.Push(command);

            // 清空 Redo 栈（执行新命令后，之前的 Redo 历史失效）
            _redoStack.Clear();

            // 限制栈大小
            if (_undoStack.Count > _maxStackSize)
            {
                var list = _undoStack.ToList();
                list.RemoveAt(list.Count - 1); // 移除最旧的命令
                _undoStack.Clear();
                foreach (var cmd in list.AsEnumerable().Reverse())
                {
                    _undoStack.Push(cmd);
                }
            }

            OnStateChanged();
        }

        /// <summary>
        /// 撤销
        /// </summary>
        public void Undo()
        {
            if (!CanUndo) return;

            var command = _undoStack.Pop();
            command.Undo();
            _redoStack.Push(command);

            OnStateChanged();
        }

        /// <summary>
        /// 重做
        /// </summary>
        public void Redo()
        {
            if (!CanRedo) return;

            var command = _redoStack.Pop();
            command.Redo();
            _undoStack.Push(command);

            OnStateChanged();
        }

        /// <summary>
        /// 清空所有历史记录
        /// </summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            OnStateChanged();
        }

        /// <summary>
        /// 获取 Undo 历史记录（用于显示历史面板）
        /// </summary>
        public IEnumerable<string> GetUndoHistory()
        {
            return _undoStack.Select(cmd => cmd.Description);
        }

        /// <summary>
        /// 获取 Redo 历史记录
        /// </summary>
        public IEnumerable<string> GetRedoHistory()
        {
            return _redoStack.Select(cmd => cmd.Description);
        }

        private void OnStateChanged()
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
