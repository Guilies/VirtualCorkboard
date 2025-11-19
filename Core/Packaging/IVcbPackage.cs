using VirtualCorkboard.Persistence.Models;
using VirtualCorkboard.Serialization;

namespace VirtualCorkboard.Packaging
{
    public interface IVcbPackage
    {
        void Save(string path, WorkspaceModel model, IWorkspaceSerializer serializer);
        WorkspaceModel Load(string path, IWorkspaceSerializer serializer);
    }
}
