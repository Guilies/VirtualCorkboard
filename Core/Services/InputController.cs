using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        public event Action? SaveRequested;
        public event Action? ToggleActiveInactiveRequested;
        public event Action? DirtyStateChanged;

        public InputController(
            Canvas notesCanvas,
            Canvas twineCanvas,
            TwineManager twineManager,
            TwineInteractionService twineInteraction)
        {
            _notesCanvas = notesCanvas ?? throw new ArgumentNullException(nameof(notesCanvas));
            _twineCanvas = twineCanvas ?? throw new ArgumentNullException(nameof(twineCanvas));
            _twineManager = twineManager ?? throw new ArgumentNullException(nameof(twineManager));
            _twineInteraction = twineInteraction ?? throw new ArgumentNullException(nameof(twineInteraction));
        }

        public void HandleKeyDown(KeyEventArgs e)
        {
            // Delete key to delete selected twine connections or notes
            if (e.Key == Key.Delete)
            {
                if (TryDeleteSelectedTwine())
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
            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                SaveRequested?.Invoke();
            }
        }

        public void HandlePreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                if (!e.Handled && TryDeleteSelectedTwine())
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
                if (TryDeleteSelectedTwine())
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
                    note.IsSelected = false;
                }
            }
        }
    }
}
