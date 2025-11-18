using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VirtualCorkboard.Twine;
using Point = System.Windows.Point;
using Application = System.Windows.Application;

namespace VirtualCorkboard.Controls
{
    public partial class PinControl : Control
    {
        private Point _lastKnownPosition;
        private WeakReference<Window>? _mainWindowRef;

        static PinControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(PinControl),
                new FrameworkPropertyMetadata(typeof(PinControl))
            );
        }

        public static readonly DependencyProperty PinColorProperty =
            DependencyProperty.Register(
                nameof(PinColor),
                typeof(Brush),
                typeof(PinControl),
                new PropertyMetadata(Brushes.Red)
            );

        public Brush PinColor
        {
            get => (Brush)GetValue(PinColorProperty);
            set => SetValue(PinColorProperty, value);
        }

        public static readonly DependencyProperty TwineColorProperty =
            DependencyProperty.Register(
                nameof(TwineColor),
                typeof(Brush),
                typeof(PinControl),
                new PropertyMetadata(Brushes.Red)
            );

        public Brush TwineColor
        {
            get => (Brush)GetValue(TwineColorProperty);
            set => SetValue(TwineColorProperty, value);
        }

        public BaseNoteControl? OwnerNote { get; set; }
        public NoteKind OwnerNoteKind { get; set; } = NoteKind.Unknown;

        public List<TwineConnection> OutgoingConnections { get; } = new();
        public List<TwineConnection> IncomingConnections { get; } = new();

        public event MouseButtonEventHandler? PinMouseDown;
        public event MouseButtonEventHandler? PinMiddleMouseDown;
        public event MouseButtonEventHandler? PinMiddleMouseUp;
        public event MouseButtonEventHandler? PinRightMouseDown;

        public PinControl()
        {
            Debug.WriteLine("[PinControl] (Core) Constructor");
            MouseDown += PinControl_MouseDown;
            MouseUp += PinControl_MouseUp;
            PreviewMouseDown += PinControl_PreviewMouseDown;
            PreviewMouseUp += PinControl_PreviewMouseUp;
        }

        private void PinControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                PinMiddleMouseDown?.Invoke(this, e);
            }
            else if (e.ChangedButton == MouseButton.Left)
            {
                PinMouseDown?.Invoke(this, e);
            }
        }

        private void PinControl_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                PinMiddleMouseUp?.Invoke(this, e);
            }
        }

        private void PinControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                PinMiddleMouseDown?.Invoke(this, e);
                e.Handled = true;
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                PinRightMouseDown?.Invoke(this, e);
                e.Handled = true;
            }
        }

        private void PinControl_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                PinMiddleMouseUp?.Invoke(this, e);
                e.Handled = true;
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (GetTemplateChild("PART_PinVisual") is UIElement pinVisual)
            {
                pinVisual.MouseLeftButtonDown += (s, e) => PinMouseDown?.Invoke(this, e);
            }
        }

        public Point GetPinPositionOnWorkspace()
        {
            if (_mainWindowRef == null || !_mainWindowRef.TryGetTarget(out var mainWindow))
            {
                mainWindow = Application.Current.MainWindow;
                _mainWindowRef = new WeakReference<Window>(mainWindow);
            }

            if (mainWindow == null || ActualWidth == 0 || ActualHeight == 0)
            {
                return _lastKnownPosition;
            }

            try
            {
                _lastKnownPosition = this.TransformToAncestor(mainWindow)
                    .Transform(new Point(ActualWidth / 2, ActualHeight / 2));
                return _lastKnownPosition;
            }
            catch
            {
                return _lastKnownPosition;
            }
        }

        // Intentionally left blank; used as a signal for layout/twine updates
        public void InvalidatePosition()
        {
        }
    }
}
