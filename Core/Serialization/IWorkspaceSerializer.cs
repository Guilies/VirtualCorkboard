using VirtualCorkboard.Persistence.Models;

namespace VirtualCorkboard.Serialization
{
    public interface IWorkspaceSerializer
    {
        string Serialize(WorkspaceModel model);
        WorkspaceModel Deserialize(string json);
    }
}
