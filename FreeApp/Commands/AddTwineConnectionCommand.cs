using System;
using VirtualCorkboard.Commands;
using VirtualCorkboard.Controls;
using VirtualCorkboard.Twine;

namespace VirtualCorkboard.Free.Commands
{
    /// <summary>
    /// Command to add a twine connection between two notes.
    /// </summary>
    public class AddTwineConnectionCommand : ICommand
    {
        private readonly TwineManager _twineManager;
        private readonly PinControl _sourcePin;
        private readonly PinControl _targetPin;
        private readonly TwineStyle _style;
        private TwineConnection? _connection;

        public string Description => "Add Connection";

        public AddTwineConnectionCommand(
            TwineManager twineManager,
            PinControl sourcePin,
            PinControl targetPin,
            TwineStyle style)
        {
            _twineManager = twineManager ?? throw new ArgumentNullException(nameof(twineManager));
            _sourcePin = sourcePin ?? throw new ArgumentNullException(nameof(sourcePin));
            _targetPin = targetPin ?? throw new ArgumentNullException(nameof(targetPin));
            _style = style ?? throw new ArgumentNullException(nameof(style));
        }

        public void Execute()
        {
            // Create connection
            _connection = new TwineConnection(_sourcePin, _targetPin, _style);
            
            // Add to TwineManager (which handles visual rendering)
            _twineManager.AddConnection(_connection);
            
            // Add to pin connection lists
            _sourcePin.OutgoingConnections.Add(_connection);
            _targetPin.IncomingConnections.Add(_connection);
        }

        public void Undo()
        {
            if (_connection != null)
            {
                _twineManager.RemoveConnection(_connection);
            }
        }

        public void Redo()
        {
            if (_connection != null)
            {
                // Re-add to TwineManager
                _twineManager.AddConnection(_connection);
                
                // Re-add to pin lists
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
}
