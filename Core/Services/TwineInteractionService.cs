using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VirtualCorkboard.Controls;
using VirtualCorkboard.Twine;

namespace VirtualCorkboard.Services
{
    /// <summary>
    /// Handles twine drag interactions without cluttering MainWindow.
    /// Coordinates between pin events, mouse tracking, and TwineManager.
    /// </summary>
    public class TwineInteractionService
    {
        private readonly Canvas _twineCanvas;
        private readonly TwineManager _twineManager;
        private readonly PinOverlayManager _pinOverlayManager;
        private readonly Window _window;

        private bool _isTwineDragging;
        private PinControl? _twineDragSourcePin;

        public event Action? TwineConnectionCompleted;
        
        /// <summary>
        /// Raised when user completes drag and pins are ready for connection.
        /// Parameters: (sourcePin, targetPin)
        /// </summary>
        public event Action<PinControl, PinControl>? TwineConnectionRequested;

        public TwineInteractionService(
            Canvas twineCanvas,
            TwineManager twineManager,
            PinOverlayManager pinOverlayManager,
            Window window)
        {
            _twineCanvas = twineCanvas ?? throw new ArgumentNullException(nameof(twineCanvas));
            _twineManager = twineManager ?? throw new ArgumentNullException(nameof(twineManager));
            _pinOverlayManager = pinOverlayManager ?? throw new ArgumentNullException(nameof(pinOverlayManager));
            _window = window ?? throw new ArgumentNullException(nameof(window));
        }

        public void StartTwineDragFromPin(PinControl pin)
        {
            if (_isTwineDragging) return;

            _twineDragSourcePin = pin;
            _isTwineDragging = true;

            var pos = pin.GetPinPositionOnWorkspace();
            _twineManager.StartTwineConnection(pin, pos);

            Mouse.Capture(_window, CaptureMode.SubTree);
            TwineConnectionCompleted?.Invoke();
        }

        public void HandleMouseMove(MouseEventArgs e)
        {
            if (_isTwineDragging && _twineDragSourcePin != null)
            {
                var pos = e.GetPosition(_twineCanvas);
                _twineManager.UpdateGhostLine(pos);
            }
        }

        public void HandleMiddleMouseUp(MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Middle) return;
            if (!_isTwineDragging || _twineDragSourcePin == null) return;

            var mousePos = e.GetPosition(_window);
            PinControl? targetPin = FindClosestPin(mousePos, 50);

            if (targetPin != null && targetPin != _twineDragSourcePin)
            {
                // Get pins from manager (which cleans up ghost line)
                var pins = _twineManager.CompleteConnection(targetPin);
                
                if (pins.HasValue)
                {
                    // Raise event with pins for command creation
                    TwineConnectionRequested?.Invoke(pins.Value.source, pins.Value.target);
                }
            }
            else
            {
                _twineManager.CancelConnection();
            }

            _isTwineDragging = false;
            _twineDragSourcePin = null;
            Mouse.Capture(null);
            e.Handled = true;

            TwineConnectionCompleted?.Invoke();
        }

        private PinControl? FindClosestPin(Point mousePos, double maxDistance)
        {
            PinControl? closestPin = null;
            double closestDist = maxDistance;

            foreach (var pin in _pinOverlayManager.EnumeratePins())
            {
                var pinPos = pin.TransformToAncestor(_window)
                    .Transform(new Point(pin.ActualWidth / 2, pin.ActualHeight / 2));
                double dist = (pinPos - mousePos).Length;

                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestPin = pin;
                }
            }

            return closestPin;
        }
    }
}
