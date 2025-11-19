using System.Collections.Generic;
using VirtualCorkboard.Persistence.Models;

namespace VirtualCorkboard.Serialization
{
    public interface IWorkspaceSnapshotSource
    {
        IEnumerable<NoteModel> CaptureNotes();
        IEnumerable<TwineConnectionModel> CaptureConnections();
        WorkspaceSettingsModel CaptureSettings();
    }
}
