using System.Windows;
using System.Windows.Controls;
using VirtualCorkboard.Controls;

namespace VirtualCorkboard.Commands
{
    /// <summary>
    /// Static helper methods for common note manipulation operations.
    /// Encapsulates repetitive patterns used across commands.
    /// </summary>
    /// <remarks>
    /// Most aren't currently in use, but might by neccessary for future commands.
    /// </remarks>
    public static class NoteCommandHelpers
    {
        /// <summary>
        /// Applies position and size to a note, updating all dependent systems.
        /// </summary>
        public static void ApplyNoteBounds(BaseNoteControl note, Rect bounds)
        {
            if (note == null) return;

            Canvas.SetLeft(note, bounds.Left);
            Canvas.SetTop(note, bounds.Top);
            note.Width = bounds.Width;
            note.Height = bounds.Height;

            InvalidateNoteVisuals(note);
        }

        /// <summary>
        /// Applies only position to a note, updating dependent systems.
        /// </summary>
        public static void ApplyNotePosition(BaseNoteControl note, double left, double top)
        {
            if (note == null) return;

            Canvas.SetLeft(note, left);
            Canvas.SetTop(note, top);

            InvalidateNoteVisuals(note);
        }

        /// <summary>
        /// Updates pin position and twine connections for a note.
        /// Should be called after any position/size change.
        /// </summary>
        public static void InvalidateNoteVisuals(BaseNoteControl note)
        {
            if (note?.Pin != null && note.TwineManager != null)
            {
                note.Pin.InvalidatePosition();
                note.TwineManager.UpdateAllConnectionsForPin(note.Pin);
            }
        }

        /// <summary>
        /// Gets the current bounds of a note on canvas.
        /// </summary>
        public static Rect GetNoteBounds(BaseNoteControl note)
        {
            double left = Canvas.GetLeft(note);
            double top = Canvas.GetTop(note);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;

            double width = note.Width > 0 ? note.Width : note.ActualWidth;
            double height = note.Height > 0 ? note.Height : note.ActualHeight;

            return new Rect(left, top, width, height);
        }
    }
}
