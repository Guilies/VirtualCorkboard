using System.Windows.Controls;
using VirtualCorkboard.Controls;
using VirtualCorkboard.Persistence.Models;
using VirtualCorkboard.Serialization;
using VirtualCorkboard.Twine;
using VirtualCorkboard.Free.Controls; // added

namespace VirtualCorkboard.Free.Serialization
{
    public class WorkspaceSnapshotProvider : IWorkspaceSnapshotSource
    {
        private readonly Canvas _notesCanvas;
        private readonly TwineManager _twineManager;

        public WorkspaceSnapshotProvider(Canvas notesCanvas, TwineManager twineManager)
        {
            _notesCanvas = notesCanvas ?? throw new ArgumentNullException(nameof(notesCanvas));
            _twineManager = twineManager ?? throw new ArgumentNullException(nameof(twineManager));
        }

        public IEnumerable<NoteModel> CaptureNotes()
        {
            foreach (var child in _notesCanvas.Children)
            {
                if (child is TextNoteControl textNote)
                {
                    double left = Canvas.GetLeft(textNote); if (double.IsNaN(left)) left =0;
                    double top = Canvas.GetTop(textNote); if (double.IsNaN(top)) top =0;
                    double width = textNote.Width >0 ? textNote.Width : textNote.ActualWidth;
                    double height = textNote.Height >0 ? textNote.Height : textNote.ActualHeight;
                    int z = Panel.GetZIndex(textNote);
                yield return new TextNoteModel
                {
                    Id = textNote.NoteId,
                    Kind = NoteKind.Text,
                    X = left,
                    Y = top,
                    Width = width,
                    Height = height,
                    Z = z,
                    Text = textNote.NoteText,
                    Format = null // future formatting capture
                    };
                }
            }
        }

        public IEnumerable<TwineConnectionModel> CaptureConnections()
        {
            foreach (var conn in _twineManager.EnumerateConnections())
            {
                var sourceId = conn.SourcePin.OwnerNote?.NoteId ?? Guid.Empty;
                var targetId = conn.TargetPin.OwnerNote?.NoteId ?? Guid.Empty;
                if (sourceId == Guid.Empty || targetId == Guid.Empty) continue;
                yield return new TwineConnectionModel
                {
                    SourceNoteId = sourceId,
                    TargetNoteId = targetId,
                    Style = new TwineStyleModel
                    {
                        Color = conn.Style.TwineColor.ToString(),
                        HighlightColor = conn.Style.HighlightColor.ToString(),
                        Texture = conn.Style.Texture,
                        Thickness = conn.Style.Thickness
                    }
                };
            }
        }

        public WorkspaceSettingsModel CaptureSettings()
        {
            // Placeholder for future zoom/pan/theme capture
            return new WorkspaceSettingsModel();
        }
    }
}
