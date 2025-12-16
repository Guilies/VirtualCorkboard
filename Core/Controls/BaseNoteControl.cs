using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using VirtualCorkboard.Twine;
using Application = System.Windows.Application;
using Panel = System.Windows.Controls.Panel;

namespace VirtualCorkboard.Controls
{
    public class BaseNoteControl : ContentControl
    {
        static BaseNoteControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(BaseNoteControl),
                new FrameworkPropertyMetadata(typeof(BaseNoteControl))
            );
        }

        // Unique identity for persistence
        public Guid NoteId
        {
            get => (Guid)GetValue(NoteIdProperty);
            set => SetValue(NoteIdProperty, value);
        }

        public static readonly DependencyProperty NoteIdProperty =
            DependencyProperty.Register(
                nameof(NoteId),
                typeof(Guid),
                typeof(BaseNoteControl),
                new PropertyMetadata(Guid.Empty)
            );

        // global z-order counter so "bring to front" always wins
        private static int _zOrderCounter = 0;
        private static int NextZ() => ++_zOrderCounter;

        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register(
                nameof(IsSelected),
                typeof(bool),
                typeof(BaseNoteControl),
                new PropertyMetadata(false, OnIsSelectedChanged)
            );

        private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BaseNoteControl note && e.NewValue is bool sel && sel)
            {
                // Whenever a note becomes selected, bring it to front
                note.BringToFront();

                // Notify host (if implemented) without referencing concrete window types
                var host = Application.Current?.MainWindow as ISelectionHost;
                host?.NoteSelected();
            }
        }

        // Allow turning bounds clamping on/off (default on)
        public bool ClampToParentBounds
        {
            get => (bool)GetValue(ClampToParentBoundsProperty);
            set => SetValue(ClampToParentBoundsProperty, value);
        }

        public static readonly DependencyProperty ClampToParentBoundsProperty =
            DependencyProperty.Register(
                nameof(ClampToParentBounds),
                typeof(bool),
                typeof(BaseNoteControl),
                new PropertyMetadata(true)
            );

        // Drag logic fields
        private bool _isDragging;
        private System.Windows.Point _dragStart;

        // Track if this note is currently in edit mode (can be overridden by derived types)
        public virtual bool IsInEditMode { get; protected set; }

        // Group move snapshot
        private readonly List<(BaseNoteControl Note, double Left, double Top)> _groupStart = new();

        // Command tracking for undo/redo
        private Rect _resizeStartBounds;

        // overlay pin injected by PinOverlayManager (no template pin lookup)
        private PinControl? _overlayPin;
        public PinControl? Pin => _overlayPin;

        internal void AttachOverlayPin(PinControl pin)
        {
            _overlayPin = pin;
            if (_overlayPin != null)
            {
                _overlayPin.OwnerNote = this;
            }
        }

        internal void DetachOverlayPin()
        {
            _overlayPin = null;
        }

        public BaseNoteControl()
        {
            // Ensure a persistent identity
            if (NoteId == Guid.Empty)
            {
                NoteId = Guid.NewGuid();
            }

            // Make the note able to receive keyboard focus
            Focusable = true;

            // Mouse events for drag logic
            MouseLeftButtonDown += BaseNoteControl_MouseLeftButtonDown;
            MouseMove += BaseNoteControl_MouseMove;
            MouseLeftButtonUp += BaseNoteControl_MouseLeftButtonUp;
            MouseDoubleClick += BaseNoteControl_MouseDoubleClick;

            // Key events for shortcuts (e.g., delete)
            KeyDown += BaseNoteControl_DeleteKeyDown;
        }

        public TwineManager? TwineManager { get; set; }

        public static readonly string[] ThumbNames =
        {
            "TopThumb", "BottomThumb", "LeftThumb", "RightThumb",
            "TopLeftThumb", "TopRightThumb", "BottomLeftThumb", "BottomRightThumb"
        };

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            foreach (var name in ThumbNames)
            {
                HookupResizeThumb(name);
            }
        }

        private void HookupResizeThumb(string thumbName)
        {
            if (Template.FindName(thumbName, this) is Thumb thumb)
            {
                thumb.DragDelta += ResizeThumb_DragDelta;
                thumb.DragStarted += ResizeThumb_DragStarted;
                thumb.DragCompleted += ResizeThumb_DragCompleted;
            }
        }

        private void ResizeThumb_DragStarted(object sender, DragStartedEventArgs e)
        {
            // Avoid pin push-back oscillations while resizing; we clamp locally
            PinOverlayManager.SuppressDuringResize = true;
            
            // Capture starting bounds for resize command
            _resizeStartBounds = GetCurrentRectOnCanvas();
        }

        private void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            PinOverlayManager.SuppressDuringResize = false;
            
            // Create resize command if bounds changed
            var newBounds = GetCurrentRectOnCanvas();
            if (!BoundsEqual(_resizeStartBounds, newBounds))
            {
                NoteResizeCompleted?.Invoke(this, _resizeStartBounds, newBounds);
            }
        }

        private static readonly Dictionary<string, ResizeEdge[]> ThumbEdges = new()
        {
            ["TopThumb"] = new[] { ResizeEdge.Top },
            ["BottomThumb"] = new[] { ResizeEdge.Bottom },
            ["LeftThumb"] = new[] { ResizeEdge.Left },
            ["RightThumb"] = new[] { ResizeEdge.Right },
            ["TopLeftThumb"] = new[] { ResizeEdge.Top, ResizeEdge.Left },
            ["TopRightThumb"] = new[] { ResizeEdge.Top, ResizeEdge.Right },
            ["BottomLeftThumb"] = new[] { ResizeEdge.Bottom, ResizeEdge.Left },
            ["BottomRightThumb"] = new[] { ResizeEdge.Bottom, ResizeEdge.Right },
        };

        private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is not Thumb thumb || !ThumbEdges.TryGetValue(thumb.Name, out var edges))
            {
                return;
            }

            double minWidth = MinWidth > 0 ? MinWidth : 50;
            double minHeight = MinHeight > 0 ? MinHeight : 50;

            foreach (var edge in edges)
            {
                ResizeFromEdge(edge, e.HorizontalChange, e.VerticalChange, minWidth, minHeight);
            }

            RequestTwineUpdate(this);
        }

        private enum ResizeEdge { Left, Right, Top, Bottom }

        // Helpers for preventing bounds overflow and pin overlap during resize
        private Rect GetCurrentRectOnCanvas()
        {
            double left = RectUtil.Coalesce(Canvas.GetLeft(this));
            double top = RectUtil.Coalesce(Canvas.GetTop(this));
            double width = RectUtil.EffectiveSize(Width, ActualWidth);
            double height = RectUtil.EffectiveSize(Height, ActualHeight);
            return new Rect(left, top, width, height);
        }

        private void ResizeFromEdge(ResizeEdge edge, double deltaX, double deltaY, double minWidth, double minHeight)
        {
            Rect current = GetCurrentRectOnCanvas();
            Rect proposed = current;
            switch (edge)
            {
                case ResizeEdge.Left:
                {
                    // Limit how far right user can drag, etc.
                    double maxLeft = current.Right - minWidth;
                    double newLeft = Math.Min(current.Left + deltaX, maxLeft);
                    proposed = RectUtil.MakeRectFromEdges(newLeft, current.Top, current.Right, current.Bottom);
                    break;
                }

                case ResizeEdge.Right:
                {
                    double minRight = current.Left + minWidth;
                    double newRight = Math.Max(current.Right + deltaX, minRight);
                    proposed = RectUtil.MakeRectFromEdges(current.Left, current.Top, newRight, current.Bottom);
                    break;
                }

                case ResizeEdge.Top:
                {
                    double maxTop = current.Bottom - minHeight;
                    double newTop = Math.Min(current.Top + deltaY, maxTop);
                    proposed = RectUtil.MakeRectFromEdges(current.Left, newTop, current.Right, current.Bottom);
                    break;
                }

                case ResizeEdge.Bottom:
                {
                    double minBottom = current.Top + minHeight;
                    double newBottom = Math.Max(current.Bottom + deltaY, minBottom);
                    proposed = RectUtil.MakeRectFromEdges(current.Left, current.Top, current.Right, newBottom);
                    break;
                }
            }

            // Enforce minimum size relative to moving edge
            proposed = RectUtil.ClampRectMinSize(proposed, minWidth, minHeight, edge);

            // Constrain to canvas bounds
            var canvas = Parent as Canvas;
            if (ClampToParentBounds)
            {
                proposed = RectUtil.ClampRectToCanvas(proposed, canvas);
            }

            // Constrain against pin exclusion rectangles
            var manager = PinOverlayManager.Instance;
            if (manager != null && canvas != null)
            {
                var pinRects = manager.GetOtherPinsExclusionRectsOnNotesCanvas(this);
                proposed = RectUtil.ClampRectAgainstPins(proposed, pinRects, minWidth, minHeight, edge);
            }

            // Apply
            Canvas.SetLeft(this, proposed.Left);
            Canvas.SetTop(this, proposed.Top);
            Width = proposed.Width;
            Height = proposed.Height;
            RaiseVisualBoundsChanged();
        }

        private void MoveGroup(Canvas canvas, System.Windows.Point currentPos)
        {
            var dx = currentPos.X - _dragStart.X;
            var dy = currentPos.Y - _dragStart.Y;

            // Clamp delta so the entire group remains within canvas bounds
            if (ClampToParentBounds)
            {
                (dx, dy) = ClampDeltaToCanvas(canvas, dx, dy);
            }

            foreach (var entry in _groupStart)
            {
                Canvas.SetLeft(entry.Note, entry.Left + dx);
                Canvas.SetTop(entry.Note, entry.Top + dy);
                RequestTwineUpdate(entry.Note);
                entry.Note.RaiseVisualBoundsChanged();
            }
        }

        // Prevents notes from being dragged outside the canvas bounds
        private (double dx, double dy) ClampDeltaToCanvas(Canvas canvas, double dx, double dy)
        {
            double canvasW = canvas.ActualWidth;
            double canvasH = canvas.ActualHeight;

            // If canvas not measured yet, skip clamping
            if (double.IsNaN(canvasW) || double.IsNaN(canvasH) || canvasW <= 0 || canvasH <= 0)
            {
                return (dx, dy);
            }

            double minDx = double.NegativeInfinity, maxDx = double.PositiveInfinity;
            double minDy = double.NegativeInfinity, maxDy = double.PositiveInfinity;

            foreach (var entry in _groupStart)
            {
                var note = entry.Note;

                double w = (!double.IsNaN(note.Width) && note.Width > 0) ? note.Width : (note.ActualWidth > 0 ? note.ActualWidth : 0);
                double h = (!double.IsNaN(note.Height) && note.Height > 0) ? note.Height : (note.ActualHeight > 0 ? note.ActualHeight : 0);

                // Allowed dx/dy for this note to stay within [0, canvasW - w] and [0, canvasH - h]
                double noteMinDx = -entry.Left;
                double noteMaxDx = canvasW - (entry.Left + w);

                double noteMinDy = -entry.Top;
                double noteMaxDy = canvasH - (entry.Top + h);

                if (noteMinDx > minDx) minDx = noteMinDx;
                if (noteMaxDx < maxDx) maxDx = noteMaxDx;

                if (noteMinDy > minDy) minDy = noteMinDy;
                if (noteMaxDy < maxDy) maxDy = noteMaxDy;
            }

            // Guard against invalid ranges caused by over-sized notes
            if (minDx > maxDx) { minDx = 0; maxDx = 0; }
            if (minDy > maxDy) { minDy = 0; maxDy = 0; }

            // Clamp requested delta to the intersection of all notes' allowed ranges
            double clampedDx = Math.Clamp(dx, minDx, maxDx);
            double clampedDy = Math.Clamp(dy, minDy, maxDy);

            return (clampedDx, clampedDy);
        }

        // Unified Twine update helper
        private static void RequestTwineUpdate(BaseNoteControl note)
        {
            if (note.TwineManager != null && note.Pin != null)
            {
                note.Pin.InvalidatePosition();
                note.TwineManager.RequestUpdateForPin(note.Pin);
            }
        }

        private void DeselectAllSiblings(Canvas canvas)
        {
            foreach (var n in EnumerateNotesInCanvas(canvas))
            {
                if (n == this) continue;
                
                // Exit edit mode for other notes (not the current one)
                n.ExitEditMode();
                
                // Only deselect if not in edit mode (in case exit didn't clear it)
                if (!n.IsInEditMode)
                {
                    n.IsSelected = false;
                }
            }
        }

        // Restored helper to enumerate sibling notes in a canvas
        private static IEnumerable<BaseNoteControl> EnumerateNotesInCanvas(Canvas canvas)
        {
            foreach (var child in canvas.Children)
            {
                if (child is BaseNoteControl n)
                {
                    yield return n;
                }
            }
        }

        private void SnapshotGroup(Canvas canvas)
        {
            _groupStart.Clear();

            // Move selected group if any selected; otherwise include self
            var group = EnumerateNotesInCanvas(canvas).Where(n => n.IsSelected).ToList();
            if (group.Count == 0)
            {
                IsSelected = true;
                group.Add(this);
            }

            foreach (var n in group)
            {
                var l = Canvas.GetLeft(n);
                var t = Canvas.GetTop(n);
                _groupStart.Add((Note: n, Left: double.IsNaN(l) ? 0 : l, Top: double.IsNaN(t) ? 0 : t));
            }
        }

        private void BringToFront()
        {
            Panel.SetZIndex(this, NextZ());
        }

        private void BringGroupToFront(Canvas canvas, BaseNoteControl preferredTop)
        {
            var group = EnumerateNotesInCanvas(canvas).Where(n => n.IsSelected).ToList();

            // raise others first
            foreach (var n in group.Where(n => n != preferredTop))
            {
                Panel.SetZIndex(n, NextZ());
            }

            // clicked note last (on top)
            Panel.SetZIndex(preferredTop, NextZ());
        }

        private int CountSelectedNotes(Canvas canvas)
        {
            int count = 0;
            foreach (var n in EnumerateNotesInCanvas(canvas))
            {
                if (n.IsSelected) count++;
            }
            return count;
        }

        private void BaseNoteControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Only start dragging if not clicking on a resize thumb
            if (e.OriginalSource is Thumb) return;
            if (e.ClickCount == 2) return; // Double-click handled separately

            if (Parent is Canvas canvas)
            {
                var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

                if (ctrl)
                {
                    // Toggle selection without affecting others
                    IsSelected = !IsSelected;

                    // Ensure this note gets keyboard focus so Delete works
                    Keyboard.Focus(this);
                    e.Handled = true;
                    return;
                }

                int selectedCount = CountSelectedNotes(canvas);
                bool isPartOfExistingMulti = IsSelected && selectedCount > 1;

                if (isPartOfExistingMulti)
                {
                    // Keep current multi-selection; begin group drag
                    BringGroupToFront(canvas, this);
                    _dragStart = e.GetPosition(canvas);
                    SnapshotGroup(canvas);
                    _isDragging = true;
                    CaptureMouse();

                    // Focus this note so Delete works during/after drag
                    Keyboard.Focus(this);
                    e.Handled = true;
                    
                    // Notify move command tracking started
                    NoteMoveStarted?.Invoke(EnumerateNotesInCanvas(canvas).Where(n => n.IsSelected).ToList());
                    return;
                }

                // Single-selection: deselect others, select this
                DeselectAllSiblings(canvas);
                IsSelected = true;

                // Focus so Delete works
                Keyboard.Focus(this);

                _dragStart = e.GetPosition(canvas);
                SnapshotGroup(canvas);
                _isDragging = true;
                CaptureMouse();
                e.Handled = true;
                
                // Notify move command tracking started
                NoteMoveStarted?.Invoke(new List<BaseNoteControl> { this });
            }
        }

        private void BaseNoteControl_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isDragging && e.LeftButton == MouseButtonState.Pressed && Parent is Canvas canvas)
            {
                var pos = e.GetPosition(canvas);
                MoveGroup(canvas, pos);
            }
        }

        private void BaseNoteControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
                e.Handled = true;

                // Final update for twine connections for the moved group
                if (Parent is Canvas canvas)
                {
                    var movedNotes = _groupStart.Select(g => g.Note).ToList();
                    
                    foreach (var entry in _groupStart)
                    {
                        if (entry.Note.TwineManager != null && entry.Note.Pin != null)
                        {
                            entry.Note.Pin.InvalidatePosition();
                            entry.Note.TwineManager.UpdateAllConnectionsForPin(entry.Note.Pin);
                        }
                    }
                    
                    // Notify move command tracking completed
                    NoteMoveCompleted?.Invoke(movedNotes);
                }

                _groupStart.Clear();
            }
        }

        private void BaseNoteControl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            EnterEditMode();
            e.Handled = true;
        }

        private void BaseNoteControl_DeleteKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Delete && Parent is Canvas canvas)
            {
                var toRemove = EnumerateNotesInCanvas(canvas).Where(n => n.IsSelected).ToList();
                if (toRemove.Count == 0)
                    return;

                // Raise event for command-based deletion
                // The actual command wrapping will be done by the handler
                NoteDeletionRequested?.Invoke(toRemove);
                
                e.Handled = true;
            }
        }

        /// <summary>
        /// Event raised when notes should be deleted.
        /// Allows MainWindow or other controllers to handle deletion via commands.
        /// </summary>
        public static event Action<System.Collections.Generic.List<BaseNoteControl>>? NoteDeletionRequested;

        public static event Action<BaseNoteControl>? NoteDeleted;

        /// <summary>
        /// Event raised when note move operation starts.
        /// </summary>
        public static event Action<System.Collections.Generic.List<BaseNoteControl>>? NoteMoveStarted;

        /// <summary>
        /// Event raised when note move operation completes.
        /// </summary>
        public static event Action<System.Collections.Generic.List<BaseNoteControl>>? NoteMoveCompleted;

        /// <summary>
        /// Event raised when note resize operation completes.
        /// </summary>
        public static event Action<BaseNoteControl, Rect, Rect>? NoteResizeCompleted;

        public event EventHandler? VisualBoundsChanged;
        internal void RaiseVisualBoundsChanged()
        {
            VisualBoundsChanged?.Invoke(this, EventArgs.Empty);
            NoteGeometryChanged?.Invoke(this); // global notification for dirty tracking
        }

        public static event Action<BaseNoteControl>? NoteGeometryChanged;

        protected virtual void EnterEditMode()
        {
            // To be overridden by derived note types
        }

        protected virtual void ExitEditMode()
        {
            IsInEditMode = false;
            // Don't automatically deselect - let derived types handle this
        }

        /// <summary>
        /// Helper to check if two Rect bounds are equal.
        /// </summary>
        private static bool BoundsEqual(Rect a, Rect b)
        {
            return DoubleUtil.AreClose(a.Left, b.Left) &&
                   DoubleUtil.AreClose(a.Top, b.Top) &&
                   DoubleUtil.AreClose(a.Width, b.Width) &&
                   DoubleUtil.AreClose(a.Height, b.Height);
        }

        // Consolidated rectangle/constraint helpers
        // These prevent notes from escaping the canvas or overlapping pin exclusion zones during resize
        private static class RectUtil
        {
            public static double Coalesce(double value, double fallback = 0) => double.IsNaN(value) ? fallback : value;

            public static double EffectiveSize(double explicitSize, double actualSize)
                => (!double.IsNaN(explicitSize) && explicitSize > 0) ? explicitSize : (actualSize > 0 ? actualSize : 0);

            public static Rect MakeRectFromEdges(double left, double top, double right, double bottom)
            {
                if (right < left) right = left;
                if (bottom < top) bottom = top;
                return new Rect(left, top, right - left, bottom - top);
            }

            public static Rect ClampRectToCanvas(Rect rect, Canvas? canvas)
            {
                if (canvas == null) return rect;
                double w = canvas.ActualWidth;
                double h = canvas.ActualHeight;
                if (double.IsNaN(w) || double.IsNaN(h) || w <= 0 || h <= 0) return rect;
                var canvasRect = new Rect(0, 0, w, h);
                // Clamp each edge within canvas
                double left = Math.Max(canvasRect.Left, rect.Left);
                double top = Math.Max(canvasRect.Top, rect.Top);
                double right = Math.Min(canvasRect.Right, rect.Right);
                double bottom = Math.Min(canvasRect.Bottom, rect.Bottom);
                return MakeRectFromEdges(left, top, right, bottom);
            }

            public static Rect ClampRectMinSize(Rect rect, double minWidth, double minHeight, ResizeEdge edge)
            {
                double w = Math.Max(rect.Width, minWidth);
                double h = Math.Max(rect.Height, minHeight);
                switch (edge)
                {
                    case ResizeEdge.Left:
                        // Keep right fixed
                        return MakeRectFromEdges(rect.Right - w, rect.Top, rect.Right, rect.Top + h);
                    case ResizeEdge.Top:
                        // Keep bottom fixed
                        return MakeRectFromEdges(rect.Left, rect.Bottom - h, rect.Right, rect.Bottom);
                    case ResizeEdge.Right:
                        // Keep left fixed
                        return new Rect(rect.Left, rect.Top, w, h);
                    case ResizeEdge.Bottom:
                        // Keep top fixed
                        return new Rect(rect.Left, rect.Top, w, h);
                    default:
                        return rect;
                }
            }

            private static bool OverlapStrict(double a1, double a2, double b1, double b2)
            {
                // True only if intervals overlap with positive length (no edge-touch count)
                double left = Math.Max(Math.Min(a1, a2), Math.Min(b1, b2));
                double right = Math.Min(Math.Max(a1, a2), Math.Max(b1, b2));
                return right - left > 0.0001; // epsilon
            }

            public static Rect ClampRectAgainstPins(Rect rect, IEnumerable<Rect> pinRects, double minWidth, double minHeight, ResizeEdge edge)
            {
                // Adjust only the actively dragged edge to avoid any overlap with pin exclusion rects
                const int maxIter = 8;
                int iter = 0;
                bool changed;
                do
                {
                    changed = false;
                    switch (edge)
                    {
                        case ResizeEdge.Left:
                        {
                            double newLeft = rect.Left;
                            foreach (var p in pinRects)
                            {
                                // If vertical ranges overlap and pin intrudes past the left edge, push left to pin.Right
                                if (OverlapStrict(rect.Top, rect.Bottom, p.Top, p.Bottom) && p.Right > newLeft && p.Left < rect.Right)
                                {
                                    newLeft = Math.Max(newLeft, p.Right);
                                }
                            }
                            if (!DoubleUtil.AreClose(newLeft, rect.Left))
                            {
                                rect = MakeRectFromEdges(newLeft, rect.Top, rect.Right, rect.Bottom);
                                // enforce min width keeping right fixed
                                if (rect.Width < minWidth)
                                {
                                    rect = MakeRectFromEdges(rect.Right - minWidth, rect.Top, rect.Right, rect.Bottom);
                                }
                                changed = true;
                            }
                        }
                        break;
                        case ResizeEdge.Right:
                        {
                            double newRight = rect.Right;
                            foreach (var p in pinRects)
                            {
                                if (OverlapStrict(rect.Top, rect.Bottom, p.Top, p.Bottom) && p.Left < newRight && p.Right > rect.Left)
                                {
                                    newRight = Math.Min(newRight, p.Left);
                                }
                            }
                            if (!DoubleUtil.AreClose(newRight, rect.Right))
                            {
                                rect = MakeRectFromEdges(rect.Left, rect.Top, newRight, rect.Bottom);
                                if (rect.Width < minWidth)
                                {
                                    rect = new Rect(rect.Left, rect.Top, minWidth, rect.Height);
                                }
                                changed = true;
                            }
                        }
                        break;
                        case ResizeEdge.Top:
                        {
                            double newTop = rect.Top;
                            foreach (var p in pinRects)
                            {
                                if (OverlapStrict(rect.Left, rect.Right, p.Left, p.Right) && p.Bottom > newTop && p.Top < rect.Bottom)
                                {
                                    newTop = Math.Max(newTop, p.Bottom);
                                }
                            }
                            if (!DoubleUtil.AreClose(newTop, rect.Top))
                            {
                                rect = MakeRectFromEdges(rect.Left, newTop, rect.Right, rect.Bottom);
                                if (rect.Height < minHeight)
                                {
                                    rect = MakeRectFromEdges(rect.Left, rect.Bottom - minHeight, rect.Right, rect.Bottom);
                                }
                                changed = true;
                            }
                        }
                        break;
                        case ResizeEdge.Bottom:
                        {
                            double newBottom = rect.Bottom;
                            foreach (var p in pinRects)
                            {
                                if (OverlapStrict(rect.Left, rect.Right, p.Left, p.Right) && p.Top < newBottom && p.Bottom > rect.Top)
                                {
                                    newBottom = Math.Min(newBottom, p.Top);
                                }
                            }
                            if (!DoubleUtil.AreClose(newBottom, rect.Bottom))
                            {
                                rect = MakeRectFromEdges(rect.Left, rect.Top, rect.Right, newBottom);
                                if (rect.Height < minHeight)
                                {
                                    rect = new Rect(rect.Left, rect.Top, rect.Width, minHeight);
                                }
                                changed = true;
                            }
                        }
                        break;
                    }
                } while (changed && ++iter < maxIter);

                return rect;
            }
        }
    }

    internal static class DoubleUtil
    {
        public static bool AreClose(double a, double b) => Math.Abs(a - b) < 0.0001;
    }
}
