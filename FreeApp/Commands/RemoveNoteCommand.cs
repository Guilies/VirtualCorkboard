using System.Windows.Controls;
using VirtualCorkboard.Commands;
using VirtualCorkboard.Controls;
using VirtualCorkboard.Twine;

namespace VirtualCorkboard.Free.Commands
{
    /// <summary>
    /// Command to remove a note from the workspace.
    /// Captures all necessary state for restoration including twine connections.
    /// </summary>
    public class RemoveNoteCommand : BaseNoteCommand
    {
        private readonly PinOverlayManager _pinOverlayManager;
        private readonly TwineManager _twineManager;
        private NoteSnapshot? _snapshot;

        public override string Description => "Delete Note";

        public RemoveNoteCommand(
            PinOverlayManager pinOverlayManager,
            TwineManager twineManager,
            Canvas canvas,
            Guid noteId)
            : base(noteId, canvas)
        {
            _pinOverlayManager = pinOverlayManager ?? throw new ArgumentNullException(nameof(pinOverlayManager));
            _twineManager = twineManager ?? throw new ArgumentNullException(nameof(twineManager));
        }

        public override void Execute()
        {
            var note = FindNote();
            if (note == null) return;

            // Capture complete state before removal
            _snapshot = NoteSnapshot.Capture(note);

            // Remove all twine connections
            if (note.Pin != null)
            {
                _twineManager.RemoveAllConnectionsForPin(note.Pin);
            }

            // Unregister pin
            _pinOverlayManager.Unregister(note);

            // Remove from canvas
            Canvas.Children.Remove(note);
        }

        public override void Undo()
        {
            if (_snapshot == null) return;

            // Recreate the note
            var note = RestoreNoteFromSnapshot(_snapshot);
            if (note == null) return;

            // Add to canvas
            Canvas.Children.Add(note);
            Canvas.SetLeft(note, _snapshot.Left);
            Canvas.SetTop(note, _snapshot.Top);
            Panel.SetZIndex(note, _snapshot.ZIndex);

            // Register pin
            var pin = _pinOverlayManager.Register(note, _snapshot.Kind);

            // Force layout update to ensure pin has correct position
            note.UpdateLayout();
            pin.UpdateLayout();
            
            // Invalidate pin position to ensure it's calculated correctly
            pin.InvalidatePosition();

            // Defer connection restoration until after visual tree is fully initialized
            // Never draw connections before notes and pins are done! stop doing it!!!
            System.Windows.Application.Current.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    // Restore twine connections (will only work if connected notes still exist)
                    foreach (var connSnapshot in _snapshot.Connections)
                    {
                        var restoredConnection = connSnapshot.Restore(_twineManager, _pinOverlayManager);
                        
                        // Update the connection position immediately to ensure correct rendering
                        if (restoredConnection != null)
                        {
                            _twineManager.UpdateConnectionPosition(restoredConnection);
                        }
                    }
                }),
                System.Windows.Threading.DispatcherPriority.Loaded
            );
        }

        public override void Redo()
        {
            // Same as Execute
            Execute();
        }

        private BaseNoteControl? RestoreNoteFromSnapshot(NoteSnapshot snapshot)
        {
            try
            {
                // Create instance of the correct note type
                var note = (BaseNoteControl)Activator.CreateInstance(snapshot.NoteType)!;
                
                // Restore basic properties
                note.NoteId = snapshot.NoteId;
                note.Width = snapshot.Width;
                note.Height = snapshot.Height;
                note.ClampToParentBounds = true;
                note.TwineManager = _twineManager;

                // Restore type-specific properties
                RestoreTypeSpecificProperties(note, snapshot);

                return note;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RemoveNoteCommand] Failed to restore note: {ex.Message}");
                return null;
            }
        }

        private void RestoreTypeSpecificProperties(BaseNoteControl note, NoteSnapshot snapshot)
        {
            // Restore TextNoteControl properties
            if (note is Free.Controls.TextNoteControl textNote && snapshot.Properties.ContainsKey("NoteText"))
            {
                textNote.NoteText = snapshot.Properties["NoteText"] as string ?? string.Empty;
            }
            
            // Future: Make use of reflection system when implemented
        }
    }
}
