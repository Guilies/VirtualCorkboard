using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VirtualCorkboard.Commands;
using VirtualCorkboard.Controls;

namespace VirtualCorkboard.Free.Commands
{
    /// <summary>
    /// Command to move one or more notes.
    /// Supports both single and multi-select moves.
    /// </summary>
    public class MoveNoteCommand : MultiNoteCommand
    {
        private readonly List<NoteMoveData> _moveData;

        public override string Description =>
            NoteIds.Count == 1 ? "Move Note" : $"Move {NoteIds.Count} Notes";

        public MoveNoteCommand(Canvas canvas, IEnumerable<BaseNoteControl> notes)
            : base(canvas, notes)
        {
            _moveData = notes.Select(n => new NoteMoveData
            {
                NoteId = n.NoteId,
                OldPosition = NoteCommandHelpers.GetNoteBounds(n).Location,
                NewPosition = NoteCommandHelpers.GetNoteBounds(n).Location
            }).ToList();
        }

        /// <summary>
        /// Update final positions after drag completes.
        /// </summary>
        public void UpdateFinalPositions(IEnumerable<BaseNoteControl> notes)
        {
            foreach (var note in notes)
            {
                var data = _moveData.FirstOrDefault(d => d.NoteId == note.NoteId);
                if (data != null)
                {
                    var bounds = NoteCommandHelpers.GetNoteBounds(note);
                    data.NewPosition = bounds.Location;
                }
            }
        }

        /// <summary>
        /// Checks if notes actually moved (prevents no-op commands).
        /// </summary>
        public bool HasMoved()
        {
            return _moveData.Any(d => 
                Math.Abs(d.OldPosition.X - d.NewPosition.X) > 0.01 ||
                Math.Abs(d.OldPosition.Y - d.NewPosition.Y) > 0.01);
        }

        public override void Execute()
        {
            // No-op: already moved during drag
        }

        public override void Undo()
        {
            ApplyPositions(_moveData.Select(d => (d.NoteId, d.OldPosition)));
        }

        public override void Redo()
        {
            ApplyPositions(_moveData.Select(d => (d.NoteId, d.NewPosition)));
        }

        private void ApplyPositions(IEnumerable<(Guid noteId, Point position)> positions)
        {
            var notesToUpdate = new List<BaseNoteControl>();
            
            foreach (var (noteId, position) in positions)
            {
                var note = FindNote(noteId);
                if (note != null)
                {
                    Canvas.SetLeft(note, position.X);
                    Canvas.SetTop(note, position.Y);
                    notesToUpdate.Add(note);
                }
            }

            // Force synchronous layout update first
            if (notesToUpdate.Count > 0)
            {
                // Update layout synchronously to ensure ActualWidth/Height are correct
                foreach (var note in notesToUpdate)
                {
                    note.UpdateLayout();
                }
                
                // Then update pins and twines after layout is stable
                Application.Current.Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        foreach (var note in notesToUpdate)
                        {
                            if (note.Pin != null)
                            {
                                // Clear cached position
                                note.Pin.InvalidatePosition();
                                PinControl? pin = note.Pin;
                                // Force pin to recalculate and update its overlay position
                                var manager = PinOverlayManager.Instance;
                                if (manager != null)
                                {
                                    manager.UpdatePinPosition(note, pin);
                                }
                            }
                            
                            // Update twine connections
                            NoteCommandHelpers.InvalidateNoteVisuals(note);
                        }
                    }),
                    System.Windows.Threading.DispatcherPriority.Render
                );
            }
        }

        private class NoteMoveData
        {
            public Guid NoteId { get; set; }
            public Point OldPosition { get; set; }
            public Point NewPosition { get; set; }
        }
    }
}
