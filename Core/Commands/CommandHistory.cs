using System;
using System.Collections.Generic;
using System.Linq;

namespace VirtualCorkboard.Commands
{
    /// <summary>
    /// Manages the undo/redo stack for workspace commands.
    /// </summary>
    public class CommandHistory
    {
        private readonly Stack<ICommand> _undoStack;
        private readonly Stack<ICommand> _redoStack;
        private readonly int _maxHistorySize;

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public event EventHandler? HistoryChanged;

        public CommandHistory(int maxHistorySize = 20)
        {
            _maxHistorySize = maxHistorySize;
            _undoStack = new Stack<ICommand>();
            _redoStack = new Stack<ICommand>();
        }

        /// <summary>
        /// Executes a command and adds it to the undo stack.
        /// Clears the redo stack.
        /// </summary>
        public void ExecuteCommand(ICommand command)
        {
            command.Execute();
            
            // Clear redo stack on new action
            _redoStack.Clear();
            
            // Add to undo stack with size limit
            _undoStack.Push(command);
            if (_undoStack.Count > _maxHistorySize)
            {
                // Remove oldest command
                var commands = _undoStack.ToArray();
                _undoStack.Clear();
                for (int i = commands.Length - 1; i >= 1; i--)
                {
                    _undoStack.Push(commands[i]);
                }
            }
            
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Undoes the most recent command.
        /// </summary>
        public void Undo()
        {
            if (!CanUndo) return;

            var command = _undoStack.Pop();
            command.Undo();
            _redoStack.Push(command);
            
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Redoes the most recently undone command.
        /// </summary>
        public void Redo()
        {
            if (!CanRedo) return;

            var command = _redoStack.Pop();
            command.Redo();
            _undoStack.Push(command);
            
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Clears all undo/redo history.
        /// </summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Gets a preview of undo/redo stacks for debugging.
        /// </summary>
        public (string[] undoDescriptions, string[] redoDescriptions) GetHistorySnapshot()
        {
            return (
                _undoStack.Select(c => c.Description).ToArray(),
                _redoStack.Select(c => c.Description).ToArray()
            );
        }
    }
}
