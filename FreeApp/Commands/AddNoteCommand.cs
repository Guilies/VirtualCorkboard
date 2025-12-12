using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VirtualCorkboard.Commands;
using VirtualCorkboard.Controls;
using VirtualCorkboard.Services;
using VirtualCorkboard.Twine;

namespace VirtualCorkboard.Free.Commands
{
    /// <summary>
    /// Command to add a new note to the workspace.
    /// Supports undo/redo of note creation.
    /// </summary>
    public class AddNoteCommand : BaseNoteCommand
    {
        private readonly NoteInteractionService _noteService;
        private readonly PinOverlayManager _pinOverlayManager;
        private readonly TwineManager _twineManager;
        private readonly Type _noteType;
        private readonly NoteKind _noteKind;
        private readonly Point _position;
        private readonly Size _size;
        private BaseNoteControl? _createdNote;
        private PinControl? _createdPin;
        private List<TwineConnectionSnapshot> _connectionSnapshots = new();

        public override string Description => $"Add {_noteKind} Note";

        /// <summary>
        /// Gets the created note after Execute() has been called.
        /// </summary>
        public BaseNoteControl? CreatedNote => _createdNote;

        public AddNoteCommand(
            NoteInteractionService noteService,
            PinOverlayManager pinOverlayManager,
            TwineManager twineManager,
            Canvas canvas,
            Type noteType,
            NoteKind noteKind,
            Point position,
            Size size)
            : base(Guid.NewGuid(), canvas)
        {
            _noteService = noteService ?? throw new ArgumentNullException(nameof(noteService));
            _pinOverlayManager = pinOverlayManager ?? throw new ArgumentNullException(nameof(pinOverlayManager));
            _twineManager = twineManager ?? throw new ArgumentNullException(nameof(twineManager));
            _noteType = noteType ?? throw new ArgumentNullException(nameof(noteType));
            _noteKind = noteKind;
            _position = position;
            _size = size;
        }

        public override void Execute()
        {
            // Create the note
            var note = (BaseNoteControl)Activator.CreateInstance(_noteType)!;
            note.NoteId = NoteId; // Use the ID from base class
            note.ClampToParentBounds = true;
            note.TwineManager = _twineManager;

            note.Width = _size.Width;
            note.Height = _size.Height;

            Canvas.Children.Add(note);
            Canvas.SetLeft(note, _position.X);
            Canvas.SetTop(note, _position.Y);

            // Register pin
            _createdPin = _pinOverlayManager.Register(note, _noteKind);

            note.IsSelected = false;
            _createdNote = note;
        }

        public override void Undo()
        {
            var note = FindNote();
            if (note != null && note.Pin != null)
            {
                // Capture any twine connections that were created after the note was added
                _connectionSnapshots.Clear();
                
                foreach (var conn in note.Pin.OutgoingConnections.ToList())
                {
                    _connectionSnapshots.Add(TwineConnectionSnapshot.Capture(conn, isOutgoing: true));
                }
                foreach (var conn in note.Pin.IncomingConnections.ToList())
                {
                    _connectionSnapshots.Add(TwineConnectionSnapshot.Capture(conn, isOutgoing: false));
                }

                // Remove all twine connections
                _twineManager.RemoveAllConnectionsForPin(note.Pin);
                
                // Unregister pin
                _pinOverlayManager.Unregister(note);
                
                // Remove from canvas
                Canvas.Children.Remove(note);
                _createdNote = note; // Keep reference for redo
            }
        }

        public override void Redo()
        {
            if (_createdNote != null)
            {
                // Re-add to canvas
                Canvas.Children.Add(_createdNote);
                
                // Re-register pin
                _createdPin = _pinOverlayManager.Register(_createdNote, _noteKind);

                // Force layout update
                _createdNote.UpdateLayout();
                _createdPin?.UpdateLayout();
                _createdPin?.InvalidatePosition();

                // Defer connection restoration until visual tree is ready
                if (_connectionSnapshots.Count > 0)
                {
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(
                        new Action(() =>
                        {
                            // Restore twine connections
                            foreach (var connSnapshot in _connectionSnapshots)
                            {
                                var restoredConnection = connSnapshot.Restore(_twineManager, _pinOverlayManager);
                                
                                if (restoredConnection != null)
                                {
                                    _twineManager.UpdateConnectionPosition(restoredConnection);
                                }
                            }
                        }),
                        System.Windows.Threading.DispatcherPriority.Loaded
                    );
                }
            }
        }
    }
}

