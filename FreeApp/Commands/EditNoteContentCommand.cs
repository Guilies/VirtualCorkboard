using System;
using System.Windows.Controls;
using VirtualCorkboard.Commands;
using VirtualCorkboard.Controls;

namespace VirtualCorkboard.Free.Commands
{
    /// <summary>
    /// Command to track edit session changes to note content.
    /// Captures entire edit session as a single undoable action.
    /// Works with any note type that implements content capture/apply.
    /// </summary>
    public class EditNoteContentCommand : BaseNoteCommand
    {
        private readonly object? _oldContent;
        private readonly object? _newContent;

        public override string Description => "Edit Note";

        public EditNoteContentCommand(
            Canvas canvas,
            Guid noteId,
            object? oldContent,
            object? newContent)
            : base(noteId, canvas)
        {
            _oldContent = oldContent;
            _newContent = newContent;
        }

        public override void Execute()
        {
            ApplyContent(_newContent);
        }

        public override void Undo()
        {
            ApplyContent(_oldContent);
        }

        public override void Redo()
        {
            ApplyContent(_newContent);
        }

        private void ApplyContent(object? content)
        {
            var note = FindNote();
            if (note != null)
            {
                // Call the note's ApplyContent method
                note.ApplyContent(content);
            }
        }
    }
}

