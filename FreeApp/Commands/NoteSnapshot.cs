using System.Windows.Controls;
using VirtualCorkboard.Controls;
using VirtualCorkboard.Twine;

namespace VirtualCorkboard.Free.Commands
{
    /// <summary>
    /// Complete snapshot of a note's state for restoration during undo operations.
    /// Captures all necessary data to fully recreate a note after deletion.
    /// </summary>
    internal class NoteSnapshot
    {
        public Guid NoteId { get; set; }
        public Type NoteType { get; set; } = typeof(BaseNoteControl);
        public NoteKind Kind { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public int ZIndex { get; set; }
        
        // Type-specific properties stored as dictionary for extensibility
        public Dictionary<string, object> Properties { get; set; } = new();
        
        // Twine connection data for restoration
        public List<TwineConnectionSnapshot> Connections { get; set; } = new();

        /// <summary>
        /// Captures complete state from a note control.
        /// </summary>
        public static NoteSnapshot Capture(BaseNoteControl note)
        {
            if (note == null)
                throw new ArgumentNullException(nameof(note));

            var snapshot = new NoteSnapshot
            {
                NoteId = note.NoteId,
                NoteType = note.GetType(),
                Kind = DetermineNoteKind(note),
                Left = Canvas.GetLeft(note),
                Top = Canvas.GetTop(note),
                Width = note.Width,
                Height = note.Height,
                ZIndex = Panel.GetZIndex(note)
            };

            // Handle NaN values for position
            if (double.IsNaN(snapshot.Left)) snapshot.Left = 0;
            if (double.IsNaN(snapshot.Top)) snapshot.Top = 0;

            // Capture type-specific properties
            CaptureTypeSpecificProperties(note, snapshot);

            // Capture twine connections
            if (note.Pin != null)
            {
                foreach (var conn in note.Pin.OutgoingConnections)
                {
                    snapshot.Connections.Add(TwineConnectionSnapshot.Capture(conn, isOutgoing: true));
                }
                foreach (var conn in note.Pin.IncomingConnections)
                {
                    snapshot.Connections.Add(TwineConnectionSnapshot.Capture(conn, isOutgoing: false));
                }
            }

            return snapshot;
        }

        private static void CaptureTypeSpecificProperties(BaseNoteControl note, NoteSnapshot snapshot)
        {
            // Capture properties specific to TextNoteControl
            if (note is Free.Controls.TextNoteControl textNote)
            {
                snapshot.Properties["NoteText"] = textNote.NoteText ?? string.Empty;
            }

            // Future: Change to reflective approach for the sake of extensibility and variable amounts of note types
        }

        /// <remarks>
        /// To be phased out in favor of reflection-based type determination.
        /// </remarks>
        private static NoteKind DetermineNoteKind(BaseNoteControl note)
        {
            // Determine kind based on type
            if (note is Free.Controls.TextNoteControl)
                return NoteKind.Text;
            
            return NoteKind.Unknown;
        }
    }

    /// <summary>
    /// Snapshot of a twine connection for restoration.
    /// </summary>
    internal class TwineConnectionSnapshot
    {
        public Guid SourceNoteId { get; set; }
        public Guid TargetNoteId { get; set; }
        public TwineStyle Style { get; set; } = new TwineStyle();
        public bool IsOutgoing { get; set; }

        public static TwineConnectionSnapshot Capture(TwineConnection connection, bool isOutgoing)
        {
            return new TwineConnectionSnapshot
            {
                SourceNoteId = connection.SourcePin.OwnerNote?.NoteId ?? Guid.Empty,
                TargetNoteId = connection.TargetPin.OwnerNote?.NoteId ?? Guid.Empty,
                Style = new TwineStyle
                {
                    TwineColor = connection.Style.TwineColor,
                    Texture = connection.Style.Texture,
                    Thickness = connection.Style.Thickness,
                    HighlightColor = connection.Style.HighlightColor
                },
                IsOutgoing = isOutgoing
            };
        }

        public TwineConnection? Restore(TwineManager twineManager, PinOverlayManager pinManager)
        {
            var sourcePin = pinManager.EnumeratePins()
                .FirstOrDefault(p => p.OwnerNote?.NoteId == SourceNoteId);
            var targetPin = pinManager.EnumeratePins()
                .FirstOrDefault(p => p.OwnerNote?.NoteId == TargetNoteId);

            if (sourcePin == null || targetPin == null)
                return null;

            // Create the connection
            var connection = new TwineConnection(sourcePin, targetPin, Style);
            
            // Add to pin connection lists 
            sourcePin.OutgoingConnections.Add(connection);
            targetPin.IncomingConnections.Add(connection);
            
            // Add to TwineManager's tracking
            twineManager.AddConnection(connection);
            
            return connection;
        }
    }
}
