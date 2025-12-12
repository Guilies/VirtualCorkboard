using System;
using System.Linq;
using System.Windows.Controls;
using VirtualCorkboard.Controls;

namespace VirtualCorkboard.Commands
{
    /// <summary>
    /// Base class for commands that operate on a single note.
    /// Provides common note reference handling.
    /// </summary>
    public abstract class BaseNoteCommand : ICommand
    {
        protected Guid NoteId { get; }
        protected Canvas Canvas { get; }
        
        public abstract string Description { get; }

        protected BaseNoteCommand(Guid noteId, Canvas canvas)
        {
            NoteId = noteId;
            Canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        }

        public abstract void Execute();
        public abstract void Undo();
        public abstract void Redo();

        protected BaseNoteControl? FindNote()
        {
            return Canvas.Children.OfType<BaseNoteControl>()
                .FirstOrDefault(n => n.NoteId == NoteId);
        }
    }
}
