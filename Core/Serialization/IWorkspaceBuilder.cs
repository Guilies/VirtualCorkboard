using VirtualCorkboard.Persistence.Models;

namespace VirtualCorkboard.Serialization
{
    public interface IWorkspaceBuilder
    {
    void ClearWorkspace();
    void CreateNote(NoteModel model);
    void FinalizeNotes();
    void CreateConnection(TwineConnectionModel connectionModel);
    void ApplySettings(WorkspaceSettingsModel settings);
    }
}
