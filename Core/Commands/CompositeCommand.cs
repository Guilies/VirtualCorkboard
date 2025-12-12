using System;
using System.Collections.Generic;
using System.Linq;

namespace VirtualCorkboard.Commands
{

    /// Groups multiple commands into a single atomic operation.
    /// All child commands execute/undo/redo together as one unit.

    /// Use cases:
    /// - Deleting multiple selected notes (each note deletion is a separate command)
    /// - Deleting multiple twine connections at once
    /// - Undoing group move operations affecting several notes

    /// Example:
    /// When user selects 5 notes and presses Delete, instead of pushing 5 separate
    /// RemoveNoteCommands to history, we create one CompositeCommand containing all 5.
    /// Now Undo restores all 5 notes in one operation.

    public class CompositeCommand : ICommand
    {
        private readonly List<ICommand> _commands;
        private readonly string _description;
        
        /// <summary>
        /// Gets the human-readable description for this composite action.
        /// </summary>
        public string Description => _description;

        /// <summary>
        /// Creates a composite command from a collection of child commands.
        /// </summary>
        /// <param name="commands">Child commands to execute as a group</param>
        /// <param name="description">Human-readable description (e.g., "Delete 5 Notes")</param>
        public CompositeCommand(IEnumerable<ICommand> commands, string description)
        {
            if (commands == null)
                throw new ArgumentNullException(nameof(commands));

            _commands = commands.ToList();
            
            if (_commands.Count == 0)
                throw new ArgumentException("CompositeCommand must contain at least one command.", nameof(commands));

            _description = description ?? $"Composite ({_commands.Count} actions)";
        }

        /// <summary>
        /// Convenience constructor that generates description automatically.
        /// </summary>
        public CompositeCommand(IEnumerable<ICommand> commands)
            : this(commands, GenerateDescription(commands))
        {
        }

        /// <summary>
        /// Executes all child commands in order.
        /// </summary>
        /// <remarks>
        /// If any command fails, execution stops. However, there is no automatic rollback.
        /// Undo must be called explicitly to reverse partial execution.
        /// </remarks>
        public void Execute()
        {
            foreach (var command in _commands)
            {
                command.Execute();
            }
        }

        /// <summary>
        /// Undoes all child commands in reverse order.
        /// </summary>
        /// <remarks>
        /// Undoing in reverse ensures proper restoration order.
        /// Example: If we added note A then B, we must remove B then A to undo correctly.
        /// </remarks>
        public void Undo()
        {
            // Undo in reverse order to maintain consistency
            for (int i = _commands.Count - 1; i >= 0; i--)
            {
                _commands[i].Undo();
            }
        }

        /// <summary>
        /// Redoes all child commands in original order.
        /// </summary>
        public void Redo()
        {
            foreach (var command in _commands)
            {
                command.Redo();
            }
        }

        /// <summary>
        /// Gets the number of child commands in this composite.
        /// </summary>
        public int CommandCount => _commands.Count;

        /// <summary>
        /// Gets a read-only view of all child commands.
        /// </summary>
        public IReadOnlyList<ICommand> Commands => _commands.AsReadOnly();

        /// <summary>
        /// Generates a user-friendly description based on child command types.
        /// </summary>
        private static string GenerateDescription(IEnumerable<ICommand> commands)
        {
            var commandList = commands.ToList();
            
            if (commandList.Count == 0)
                return "Empty Composite";

            if (commandList.Count == 1)
                return commandList[0].Description;

            // Group by command type
            var groups = commandList
                .GroupBy(c => c.GetType().Name)
                .OrderByDescending(g => g.Count())
                .ToList();

            if (groups.Count == 1)
            {
                // All commands are the same type
                var typeName = SimplifyCommandName(groups[0].Key);
                return $"{typeName} ({commandList.Count} items)";
            }

            // Mixed command types
            return $"Composite Action ({commandList.Count} commands)";
        }

        /// <summary>
        /// Simplifies command type name for display.
        /// Example: "RemoveNoteCommand" -> "Delete Note"
        /// </summary>
        private static string SimplifyCommandName(string typeName)
        {
            // Remove "Command" suffix
            if (typeName.EndsWith("Command"))
                typeName = typeName.Substring(0, typeName.Length - "Command".Length);

            // Insert spaces before capitals
            var result = System.Text.RegularExpressions.Regex.Replace(
                typeName,
                "([a-z])([A-Z])",
                "$1 $2"
            );

            return result;
        }

        /// <summary>
        /// Helper method to determine if all child commands are of the same type.
        /// </summary>
        public bool IsHomogeneous()
        {
            if (_commands.Count <= 1)
                return true;

            var firstType = _commands[0].GetType();
            return _commands.All(c => c.GetType() == firstType);
        }
    }
}
