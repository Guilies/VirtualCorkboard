using System;
using VirtualCorkboard.Commands;

namespace VirtualCorkboard.Services
{
    /// <summary>
    /// Central service for managing command execution and history.
    /// Integrates with WorkspaceController for dirty state tracking.
    /// </summary>
    public class CommandManager
    {
        private readonly CommandHistory _history;
        private readonly IWorkspaceDirtyState? _dirtyStateTracker;

        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;

        public event EventHandler? CommandExecuted;
        public event EventHandler? HistoryChanged;

        public CommandManager(
            IWorkspaceDirtyState? dirtyStateTracker = null,
            int maxHistorySize = 20)
        {
            _history = new CommandHistory(maxHistorySize);
            _dirtyStateTracker = dirtyStateTracker;

            _history.HistoryChanged += (s, e) =>
            {
                HistoryChanged?.Invoke(this, EventArgs.Empty);
            };
        }

        /// <summary>
        /// Executes a command and adds it to history.
        /// </summary>
        public void Execute(ICommand command)
        {
            _history.ExecuteCommand(command);
            _dirtyStateTracker?.MarkDirty();
            CommandExecuted?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Undoes the most recent command.
        /// </summary>
        public void Undo()
        {
            if (_history.CanUndo)
            {
                _history.Undo();
                _dirtyStateTracker?.MarkDirty();
            }
        }

        /// <summary>
        /// Redoes the most recently undone command.
        /// </summary>
        public void Redo()
        {
            if (_history.CanRedo)
            {
                _history.Redo();
                _dirtyStateTracker?.MarkDirty();
            }
        }

        /// <summary>
        /// Allows clearing all command history if needed.
        /// </summary>
        public void ClearHistory()
        {
            _history.Clear();
        }

        /// <summary>
        /// For debug information about current history state.
        /// </summary>
        public (string[] undoStack, string[] redoStack) GetHistorySnapshot()
        {
            return _history.GetHistorySnapshot();
        }
    }
}
