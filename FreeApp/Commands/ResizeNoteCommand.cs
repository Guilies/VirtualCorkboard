using System;
using System.Windows;
using System.Windows.Controls;
using VirtualCorkboard.Commands;
using VirtualCorkboard.Controls;

namespace VirtualCorkboard.Free.Commands
{
    /// <summary>
    /// Command to resize a note.
    /// </summary>
    public class ResizeNoteCommand : BaseNoteCommand
    {
        private readonly Rect _oldBounds;
        private readonly Rect _newBounds;

        public override string Description => "Resize Note";

        public ResizeNoteCommand(
            Canvas canvas,
            Guid noteId,
            Rect oldBounds,
            Rect newBounds)
            : base(noteId, canvas)
        {
            _oldBounds = oldBounds;
            _newBounds = newBounds;
        }

        public override void Execute()
        {
            var note = FindNote();
            if (note != null)
            {
                NoteCommandHelpers.ApplyNoteBounds(note, _newBounds);
            }
        }

        public override void Undo()
        {
            var note = FindNote();
            if (note != null)
            {
                ApplyBoundsWithDeferredUpdate(note, _oldBounds);
            }
        }

        public override void Redo()
        {
            var note = FindNote();
            if (note != null)
            {
                ApplyBoundsWithDeferredUpdate(note, _newBounds);
            }
        }

        /// <summary>
        /// Applies bounds and defers pin/twine updates until after layout.
        /// </summary>
        private void ApplyBoundsWithDeferredUpdate(BaseNoteControl note, Rect bounds)
        {
            Canvas.SetLeft(note, bounds.Left);
            Canvas.SetTop(note, bounds.Top);
            note.Width = bounds.Width;
            note.Height = bounds.Height;

            // Force synchronous layout update first
            note.UpdateLayout();

            // Then update pins and twines after layout is stable
            System.Windows.Application.Current.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    if (note.Pin != null)
                    {
                        // Clear cached position
                        note.Pin.InvalidatePosition();
                        
                        // Force pin to recalculate and update its overlay position
                        var manager = PinOverlayManager.Instance;
                        if (manager != null)
                        {
                            manager.UpdatePinPosition(note, note.Pin);
                        }
                    }
                    
                    // Update twine connections
                    NoteCommandHelpers.InvalidateNoteVisuals(note);
                }),
                System.Windows.Threading.DispatcherPriority.Render
            );
        }
    }
}
