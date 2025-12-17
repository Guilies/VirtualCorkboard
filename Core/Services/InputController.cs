using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VirtualCorkboard.Commands;
using VirtualCorkboard.Controls;
using VirtualCorkboard.Twine;

namespace VirtualCorkboard.Services
{
    /// <summary>
    /// Centralizes all input event handling for MainWindow.
    /// Routes key presses, mouse clicks, and delegates to appropriate handlers.
    /// </summary>
    public class InputController
    {
        private readonly Canvas _notesCanvas;
        private readonly Canvas _twineCanvas;
        private readonly TwineManager _twineManager;
        private readonly TwineInteractionService _twineInteraction;
        private readonly CommandManager _commandManager;

        public event Action? SaveRequested;
        public event Action? ToggleActiveInactiveRequested;
        public event Action? DirtyStateChanged;
        public event Action? TwineDeletionRequested;

        public InputController(
            Canvas notesCanvas,
            Canvas twineCanvas,
            TwineManager twineManager,
            TwineInteractionService twineInteraction,
            CommandManager commandManager)
        {
            _notesCanvas = notesCanvas ?? throw new ArgumentNullException(nameof(notesCanvas));
            _twineCanvas = twineCanvas ?? throw new ArgumentNullException(nameof(twineCanvas));
            _twineManager = twineManager ?? throw new ArgumentNullException(nameof(twineManager));
            _twineInteraction = twineInteraction ?? throw new ArgumentNullException(nameof(twineInteraction));
            _commandManager = commandManager ?? throw new ArgumentNullException(nameof(commandManager));
        }

        public void HandleKeyDown(KeyEventArgs e)
        {
            var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            var shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            // Undo: Ctrl+Z (without Shift)
            if (ctrl && !shift && e.Key == Key.Z)
            {
                _commandManager.Undo();
                e.Handled = true;
                return;
            }

            // Redo: Ctrl+Shift+Z
            if (ctrl && shift && e.Key == Key.Z)
            {
                _commandManager.Redo();
                e.Handled = true;
                return;
            }

            // Delete key to delete selected twine connections or notes
            if (e.Key == Key.Delete)
            {
                if (TryDeleteSelectedTwineViaCommand())
                {
                    e.Handled = true;
                    DirtyStateChanged?.Invoke();
                    return;
                }
            }

            // F1 to toggle active/inactive
            if (e.Key == Key.F1)
            {
                ToggleActiveInactiveRequested?.Invoke();
            }

            // Ctrl+S to save
            if (e.Key == Key.S && ctrl)
            {
                SaveRequested?.Invoke();
            }
        }

        public void HandlePreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                if (!e.Handled && TryDeleteSelectedTwineViaCommand())
                {
                    e.Handled = true;
                    DirtyStateChanged?.Invoke();
                }
            }
        }

        public void HandleTwineCanvasKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                if (TryDeleteSelectedTwineViaCommand())
                {
                    e.Handled = true;
                    DirtyStateChanged?.Invoke();
                }
            }
        }

        public void HandleMouseMove(MouseEventArgs e)
        {
            _twineInteraction.HandleMouseMove(e);
        }

        public void HandlePreviewMouseUp(MouseButtonEventArgs e)
        {
            _twineInteraction.HandleMiddleMouseUp(e);
        }

        public void HandleNotesCanvasMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (ReferenceEquals(e.Source, _notesCanvas))
            {
                ClearAllNoteSelections();
                _twineManager.ClearSelection();
            }
        }

        public void HandleTwineCanvasMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (ReferenceEquals(e.Source, _twineCanvas))
            {
                ClearAllNoteSelections();
            }
        }

        private bool TryDeleteSelectedTwineViaCommand()
        {
            if (_twineManager != null && _twineManager.HasSelectedConnections)
            {
                // Raise event for command-based deletion
                TwineDeletionRequested?.Invoke();
                return true;
            }
            return false;
        }

        // Legacy method - kept for compatibility but should not be used
        private bool TryDeleteSelectedTwine()
        {
            if (_twineManager != null && _twineManager.HasSelectedConnections)
            {
                _twineManager.DeleteSelectedConnections();
                return true;
            }
            return false;
        }

        private void ClearAllNoteSelections()
        {
            foreach (var child in _notesCanvas.Children)
            {
                if (child is BaseNoteControl note)
                {
                    // Don't deselect notes that are currently in edit mode
                    // This prevents the note from being deselected when clicked while editing
                    if (!note.IsInEditMode)
                    {
                        note.IsSelected = false;
                    }
                }
            }
        }
    }
}
