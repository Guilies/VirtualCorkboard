using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace VirtualCorkboard.Services
{
    /// <summary>
    /// Handles zooming and panning of a workspace canvas inside a ScrollViewer.
    /// Zoom is exponential (Ctrl+Wheel). Pan is Space+LeftMouse drag.
    /// Viewport is clamped dynamically based on canvas size, viewport size,
    /// zoom level, and padding ratio (similar to Clip Studio Paint).
    /// </summary>
    public class WorkspaceViewportService
    {
        private readonly FrameworkElement _canvas;      // Logical workspace
        private readonly FrameworkElement _transformTarget; // Element receiving transforms
        private readonly ScrollViewer _scrollViewer;

        private readonly ScaleTransform _scaleTransform;
        private readonly TranslateTransform _translateTransform;
        private readonly TransformGroup _transformGroup;

        // Zoom config
        private const double MinZoom = 0.10;     // 10%
        private const double MaxZoom = 8.00;     // 800%
        private const double ZoomFactor = 1.20;  // Exponential zoom multiplier

        // Viewport padding (CSP-style)
        // When zoomed out, user can drag far. When zoomed in, drag is restricted.
        private const double PaddingRatio = 0.25; // 25% of viewport

        // Pan state
        private bool _isPanning;
        private Point _panStart;
        private Point _panOrigin;

        public double ZoomLevel => _scaleTransform.ScaleX;

        public event EventHandler<double>? ZoomChanged;
        public event EventHandler<Point>? PanChanged;

        public WorkspaceViewportService(
            FrameworkElement canvas,
            FrameworkElement transformTarget,
            ScrollViewer scrollViewer)
        {
            _canvas = canvas;
            _transformTarget = transformTarget;
            _scrollViewer = scrollViewer;

            _scaleTransform = new ScaleTransform(1, 1);
            _translateTransform = new TranslateTransform(0, 0);

            _transformGroup = new TransformGroup();
            _transformGroup.Children.Add(_scaleTransform);
            _transformGroup.Children.Add(_translateTransform);

            transformTarget.RenderTransform = _transformGroup;
            transformTarget.RenderTransformOrigin = new Point(0, 0);
        }

        // Zoom Handling
        public void HandleMouseWheel(MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
                return;

            Point mousePos = e.GetPosition(_scrollViewer);

            double oldZoom = _scaleTransform.ScaleX;
            double newZoom =
                e.Delta > 0 ? oldZoom * ZoomFactor : oldZoom / ZoomFactor;

            newZoom = Math.Clamp(newZoom, MinZoom, MaxZoom);

            ZoomToPoint(mousePos, newZoom);

            e.Handled = true;
        }

        private void ZoomToPoint(Point viewportPoint, double newZoom)
        {
            double oldZoom = _scaleTransform.ScaleX;

            // Convert viewport → workspace coordinates
            double workspaceX = (viewportPoint.X - _translateTransform.X) / oldZoom;
            double workspaceY = (viewportPoint.Y - _translateTransform.Y) / oldZoom;

            // Apply zoom
            _scaleTransform.ScaleX = newZoom;
            _scaleTransform.ScaleY = newZoom;

            // Recompute translation so same workspace point stays under cursor
            double newTranslateX = viewportPoint.X - workspaceX * newZoom;
            double newTranslateY = viewportPoint.Y - workspaceY * newZoom;

            _translateTransform.X = newTranslateX;
            _translateTransform.Y = newTranslateY;

            ClampPan(null);

            ZoomChanged?.Invoke(this, newZoom);
            PanChanged?.Invoke(this, new Point(_translateTransform.X, _translateTransform.Y));
        }

        // Panning
        public void HandleMouseDown(MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && Keyboard.IsKeyDown(Key.Space))
            {
                StartPan(e.GetPosition(_scrollViewer));
                e.Handled = true;
            }
        }

        public void HandleMouseUp(MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && _isPanning)
            {
                EndPan();
                e.Handled = true;
            }
        }

        public void HandleMouseMove(MouseEventArgs e)
        {
            if (_isPanning && e.LeftButton == MouseButtonState.Pressed)
            {
                UpdatePan(e.GetPosition(_scrollViewer));
                e.Handled = true;
            }
        }

        private void StartPan(Point mousePos)
        {
            _isPanning = true;
            _panStart = mousePos;
            _panOrigin = new Point(_translateTransform.X, _translateTransform.Y);
            _scrollViewer.Cursor = Cursors.SizeAll;
        }

        private void UpdatePan(Point currentPos)
        {
            if (!_isPanning) return;

            double deltaX = currentPos.X - _panStart.X;
            double deltaY = currentPos.Y - _panStart.Y;

            _translateTransform.X = _panOrigin.X + deltaX;
            _translateTransform.Y = _panOrigin.Y + deltaY;

            ClampPan(new Point(deltaX, deltaY));

            PanChanged?.Invoke(this, new Point(_translateTransform.X, _translateTransform.Y));
        }


        private void EndPan()
        {
            _isPanning = false;
            _scrollViewer.Cursor = Cursors.Arrow;
        }


        // Viewport Clamping
        private void ClampPan(Point? lastDelta = null)
        {
            double zoom = _scaleTransform.ScaleX;

            double scaledW = _canvas.ActualWidth * zoom;
            double scaledH = _canvas.ActualHeight * zoom;

            double viewW = _scrollViewer.ActualWidth;
            double viewH = _scrollViewer.ActualHeight;

            double padX = viewW * PaddingRatio;
            double padY = viewH * PaddingRatio;

            // Canvas edges IN VIEWPORT COORDINATES
            double left = _translateTransform.X;
            double right = _translateTransform.X + scaledW;
            double top = _translateTransform.Y;
            double bottom = _translateTransform.Y + scaledH;

            // Soft boundaries (25% padding)
            double minVisibleX = padX;
            double maxVisibleX = viewW - padX;
            double minVisibleY = padY;
            double maxVisibleY = viewH - padY;

            // Determine pan direction (if delta provided from mouse movement)
            double dx = lastDelta?.X ?? 0;
            double dy = lastDelta?.Y ?? 0;

            // Horizontal clamp
            if (dx < 0)
            {
                // Panning left → canvas moves right → clamp RIGHT edge
                if (right < minVisibleX)
                    _translateTransform.X += (minVisibleX - right);
            }
            else if (dx > 0)
            {
                // Panning right → canvas moves left → clamp LEFT edge
                if (left > maxVisibleX)
                    _translateTransform.X -= (left - maxVisibleX);
            }

            // Vertical clamp
            if (dy < 0)
            {
                // Panning up -> canvas moves down -> clamp BOTTOM edge
                if (bottom < minVisibleY)
                    _translateTransform.Y += (minVisibleY - bottom);
            }
            else if (dy > 0)
            {
                // Panning down -> canvas moves up -> clamp TOP edge
                if (top > maxVisibleY)
                    _translateTransform.Y -= (top - maxVisibleY);
            }
        }



        // Manual Controls
        public void SetZoom(double zoom)
        {
            double newZoom = Math.Clamp(zoom, MinZoom, MaxZoom);
            _scaleTransform.ScaleX = newZoom;
            _scaleTransform.ScaleY = newZoom;
            ClampPan();
            ZoomChanged?.Invoke(this, newZoom);
        }

        public void SetPan(double x, double y)
        {
            _translateTransform.X = x;
            _translateTransform.Y = y;
            ClampPan();
            PanChanged?.Invoke(this, new Point(_translateTransform.X, _translateTransform.Y));
        }

        public void ResetView()
        {
            SetZoom(1.0);
            SetPan(0, 0);
        }

        // Persistence
        public (double zoom, double panX, double panY) GetViewportState()
        {
            return (_scaleTransform.ScaleX, _translateTransform.X, _translateTransform.Y);
        }

        public void RestoreViewportState(double zoom, double panX, double panY)
        {
            SetZoom(zoom);
            SetPan(panX, panY);
        }
    }
}
