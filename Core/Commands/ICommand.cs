namespace VirtualCorkboard.Commands
{
    /// <summary>
    /// Represents an undoable/redoable action in the workspace.
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// Executes the command for the first time.
        /// </summary>
        void Execute();

        /// <summary>
        /// Reverses the command's effects.
        /// </summary>
        void Undo();

        /// <summary>
        /// Re-applies the command after it has been undone.
        /// </summary>
        void Redo();

        /// <summary>
        /// Human-readable description of the command for debugging.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Indicates whether this command can be merged with a subsequent similar command.
        /// </summary>
        // Below are mainly just in case it's needed later. Better safe than sorry!
        bool CanMergeWith(ICommand other) => false;

        /// <summary>
        /// Merges this command with another compatible command.
        /// </summary>
        void MergeWith(ICommand other) { }
    }
}
