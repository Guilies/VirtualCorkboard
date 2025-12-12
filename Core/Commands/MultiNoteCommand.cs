using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using VirtualCorkboard.Controls;

namespace VirtualCorkboard.Commands
{
    /// <summary>
    /// Base class for commands that operate on multiple notes simultaneously.
    /// Provides common infrastructure for group operations.
    /// </summary>
    public abstract class MultiNoteCommand : ICommand
    {
        protected Canvas Canvas { get; }
        protected IReadOnlyList<Guid> NoteIds { get; }

        public abstract string Description { get; }

        protected MultiNoteCommand(Canvas canvas, IEnumerable<Guid> noteIds)
        {
            Canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            NoteIds = noteIds?.ToList() ?? throw new ArgumentNullException(nameof(noteIds));
            
            if (NoteIds.Count == 0)
                throw new ArgumentException("Must operate on at least one note.", nameof(noteIds));
        }

        /// <summary>
        /// Alternative constructor accepting note controls directly.
        /// </summary>
        // Not currently in use, but included for potential future convenience.
        protected MultiNoteCommand(Canvas canvas, IEnumerable<BaseNoteControl> notes)
            : this(canvas, notes?.Select(n => n.NoteId) ?? Enumerable.Empty<Guid>())
        {
        }

        public abstract void Execute();
        public abstract void Undo();
        public abstract void Redo();

        /// <summary>
        /// Finds all notes referenced by this command.
        /// Returns only notes that currently exist in the canvas.
        /// </summary>
        protected IEnumerable<BaseNoteControl> FindNotes()
        {
            var noteMap = Canvas.Children.OfType<BaseNoteControl>()
                .ToDictionary(n => n.NoteId);

            foreach (var id in NoteIds)
            {
                if (noteMap.TryGetValue(id, out var note))
                    yield return note;
            }
        }

        /// <summary>
        /// Finds a specific note by ID.
        /// </summary>
        protected BaseNoteControl? FindNote(Guid noteId)
        {
            return Canvas.Children.OfType<BaseNoteControl>()
                .FirstOrDefault(n => n.NoteId == noteId);
        }
    }
}
