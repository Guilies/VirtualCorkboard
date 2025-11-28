using System;
using System.Windows;
using System.Windows.Controls;
using VirtualCorkboard.Controls;
using VirtualCorkboard.Twine;

namespace VirtualCorkboard.Services
{
    /// <summary>
    /// Manages note creation, registration, and event wiring.
    /// Keeps note-specific logic out of MainWindow.
    /// </summary>
    public class NoteInteractionService
    {
        private readonly Canvas _notesCanvas;
        private readonly TwineManager _twineManager;
        private readonly PinOverlayManager _pinOverlayManager;

        public event Action? NoteModified;

        public NoteInteractionService(
            Canvas notesCanvas,
            TwineManager twineManager,
            PinOverlayManager pinOverlayManager)
        {
            _notesCanvas = notesCanvas ?? throw new ArgumentNullException(nameof(notesCanvas));
            _twineManager = twineManager ?? throw new ArgumentNullException(nameof(twineManager));
            _pinOverlayManager = pinOverlayManager ?? throw new ArgumentNullException(nameof(pinOverlayManager));
        }

        public T CreateNote<T>(NoteKind kind, double? left = null, double? top = null) where T : BaseNoteControl, new()
        {
            var note = new T
            {
                ClampToParentBounds = true,
                TwineManager = _twineManager
            };

            _notesCanvas.Children.Add(note);

            // Center by default if no position specified
            double canvasW = _notesCanvas.ActualWidth;
            double canvasH = _notesCanvas.ActualHeight;

            double finalLeft = left ?? (double.IsNaN(canvasW) || canvasW <= 0 ? 100 : (canvasW - note.Width) / 2.0);
            double finalTop = top ?? (double.IsNaN(canvasH) || canvasH <= 0 ? 100 : (canvasH - note.Height) / 2.0);

            Canvas.SetLeft(note, finalLeft);
            Canvas.SetTop(note, finalTop);

            // Register with pin overlay manager
            _pinOverlayManager.Register(note, kind);

            note.IsSelected = false;
            note.VisualBoundsChanged += OnNoteVisualBoundsChanged;

            NoteModified?.Invoke();

            return note;
        }

        private void OnNoteVisualBoundsChanged(object? sender, EventArgs e)
        {
            NoteModified?.Invoke();
        }
    }
}
