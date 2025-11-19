using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using VirtualCorkboard.Controls;
using VirtualCorkboard.Free.Controls;
using VirtualCorkboard.Persistence.Models;
using VirtualCorkboard.Serialization;
using VirtualCorkboard.Twine;

namespace VirtualCorkboard.Free.Serialization
{
    public class WorkspaceUiBuilder : IWorkspaceBuilder
        {
        private readonly Canvas _notesCanvas;
        private readonly TwineManager _twineManager;
        private readonly PinOverlayManager _pinOverlayManager;
        private readonly List<BaseNoteControl> _createdNotes = new();
        private readonly Dictionary<Guid, BaseNoteControl> _idToNote = new();

        public WorkspaceUiBuilder(Canvas notesCanvas, TwineManager twineManager, PinOverlayManager pinOverlayManager)
        {
            _notesCanvas = notesCanvas;
            _twineManager = twineManager;
            _pinOverlayManager = pinOverlayManager;
        }

        public void ClearWorkspace()
        {
            var toRemove = _notesCanvas.Children.OfType<BaseNoteControl>().ToList();
            foreach (var note in toRemove)
            {
                if (note.Pin != null && note.TwineManager != null)
                {
                    note.TwineManager.RemoveAllConnectionsForPin(note.Pin);
                }
                _pinOverlayManager.Unregister(note);
                _notesCanvas.Children.Remove(note);
            }
            _createdNotes.Clear();
            _idToNote.Clear();
        }

        public void CreateNote(NoteModel model)
        {
            BaseNoteControl note;
            switch (model.Kind)
            {
                case NoteKind.Text:
                var tm = model as TextNoteModel;
                note = new TextNoteControl
                {
                    NoteText = tm?.Text ?? string.Empty,
                    Width = tm?.Width ?? model.Width,
                    Height = tm?.Height ?? model.Height
                };
                break;
                default:
                // Fallback placeholder
                note = new TextNoteControl { NoteText = $"Unsupported note kind: {model.Kind}" }; break;
            }

            note.NoteId = model.Id != Guid.Empty ? model.Id : Guid.NewGuid();
            note.TwineManager = _twineManager;
            _notesCanvas.Children.Add(note);
            Canvas.SetLeft(note, model.X);
            Canvas.SetTop(note, model.Y);
            Panel.SetZIndex(note, model.Z);
            _pinOverlayManager.Register(note, model.Kind);
            _createdNotes.Add(note);
            _idToNote[note.NoteId] = note;
        }

        public void FinalizeNotes()
        {
            // Future: layout pass / dispatcher if needed
        }

        public void CreateConnection(TwineConnectionModel connectionModel)
        {
            if (!_idToNote.TryGetValue(connectionModel.SourceNoteId, out var source)) return;
            if (!_idToNote.TryGetValue(connectionModel.TargetNoteId, out var target)) return;
            var sourcePin = source.Pin; var targetPin = target.Pin;
            if (sourcePin == null || targetPin == null) return;

            var style = new TwineStyle
            {
                TwineColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(connectionModel.Style.Color),
                HighlightColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(connectionModel.Style.HighlightColor),
                Texture = connectionModel.Style.Texture,
                Thickness = connectionModel.Style.Thickness
            };

            var conn = new TwineConnection(sourcePin, targetPin, style);
            sourcePin.OutgoingConnections.Add(conn);
            targetPin.IncomingConnections.Add(conn);
            _twineManager.AddConnection(conn);
        }

        public void ApplySettings(WorkspaceSettingsModel settings)
        {
            // Placeholder for zoom/pan/theme restore
        }
    }
}
