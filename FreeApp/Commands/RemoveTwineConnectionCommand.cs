using System;
using VirtualCorkboard.Commands;
using VirtualCorkboard.Controls;
using VirtualCorkboard.Twine;

namespace VirtualCorkboard.Free.Commands
{
    /// <summary>
    /// Command to remove a twine connection.
    /// Captures connection state for restoration.
    /// </summary>
    public class RemoveTwineConnectionCommand : ICommand
    {
        private readonly TwineManager _twineManager;
        private readonly TwineConnectionCommandSnapshot _snapshot;

        public string Description => "Remove Connection";

        public RemoveTwineConnectionCommand(
            TwineManager twineManager,
            TwineConnection connection)
        {
            _twineManager = twineManager ?? throw new ArgumentNullException(nameof(twineManager));
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            
            _snapshot = new TwineConnectionCommandSnapshot(connection);
        }

        public void Execute()
        {
            var connection = _snapshot.GetConnection();
            if (connection != null)
            {
                _twineManager.RemoveConnection(connection);
            }
        }

        public void Undo()
        {
            _snapshot.Restore(_twineManager);
        }

        public void Redo()
        {
            Execute();
        }
    }

    /// <summary>
    /// Snapshot of a twine connection for command restoration.
    /// </summary>
    internal class TwineConnectionCommandSnapshot
    {
        private readonly PinControl _sourcePin;
        private readonly PinControl _targetPin;
        private readonly TwineStyle _style;
        private TwineConnection? _connection;

        public TwineConnectionCommandSnapshot(TwineConnection connection)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            
            _sourcePin = connection.SourcePin;
            _targetPin = connection.TargetPin;
            _style = connection.Style;
            _connection = connection;
        }

        public TwineConnection? GetConnection()
        {
            return _connection;
        }

        public void Restore(TwineManager twineManager)
        {
            // Recreate connection if it doesn't exist
            if (_connection == null || !_sourcePin.OutgoingConnections.Contains(_connection))
            {
                _connection = new TwineConnection(_sourcePin, _targetPin, _style);
            }

            // Add to manager
            twineManager.AddConnection(_connection);
            
            // Add to pin lists if not already there
            if (!_sourcePin.OutgoingConnections.Contains(_connection))
            {
                _sourcePin.OutgoingConnections.Add(_connection);
            }
            if (!_targetPin.IncomingConnections.Contains(_connection))
            {
                _targetPin.IncomingConnections.Add(_connection);
            }
        }
    }
}

