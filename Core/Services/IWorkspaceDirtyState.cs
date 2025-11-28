namespace VirtualCorkboard.Services
{
    /// <summary>
    /// Provides access to workspace dirty state for services that need to monitor it.
    /// </summary>
    public interface IWorkspaceDirtyState
    {
        bool IsDirty { get; }
        bool SuppressDirtyForNewWorkspace { get; set; }
    }
}
