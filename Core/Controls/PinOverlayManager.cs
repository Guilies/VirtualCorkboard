using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace VirtualCorkboard.Controls
{
 public class PinOverlayManager
 {
 private readonly Canvas _pinsCanvas;
 private readonly Canvas _notesCanvas;
 private readonly Dictionary<BaseNoteControl, PinControl> _noteToPin = new();
 public static PinOverlayManager? Instance { get; private set; }
 public static bool SuppressDuringResize { get; set; }
 public double PinHorizontalOffset { get; set; } =0;
 public double PinVerticalOffset { get; set; } =15;
 public double PinExclusionPadding { get; set; } =4;
 private bool _resolvingOverlap;

 // Raised when a pin is middle-clicked (request to start twine drag)
 public event Action<PinControl>? PinMiddleDragRequested;

 public PinOverlayManager(Canvas pinsCanvas, Canvas notesCanvas)
 {
 _pinsCanvas = pinsCanvas ?? throw new ArgumentNullException(nameof(pinsCanvas));
 _notesCanvas = notesCanvas ?? throw new ArgumentNullException(nameof(notesCanvas));
 Instance = this;
 }

 public PinControl Register(BaseNoteControl note, NoteKind kind)
 {
 if (note == null) throw new ArgumentNullException(nameof(note));
 if (_noteToPin.ContainsKey(note)) return _noteToPin[note];
 var pin = new PinControl { Width =18, Height =18, OwnerNote = note, OwnerNoteKind = kind };
 _noteToPin[note] = pin;
 _pinsCanvas.Children.Add(pin);
 note.AttachOverlayPin(pin);
 note.Loaded += NoteOnLoadedOrChanged;
 note.SizeChanged += NoteOnLoadedOrChanged;
 note.VisualBoundsChanged += NoteOnLoadedOrChanged;
 note.Unloaded += NoteOnUnloaded;
 pin.PinMiddleMouseDown += Pin_PinMiddleMouseDown;
 EnforcePinExclusionFor(note);
 UpdatePinPosition(note, pin);
 return pin;
 }

 public void Unregister(BaseNoteControl note)
 {
 if (!_noteToPin.TryGetValue(note, out var pin)) return;
 note.Loaded -= NoteOnLoadedOrChanged;
 note.SizeChanged -= NoteOnLoadedOrChanged;
 note.VisualBoundsChanged -= NoteOnLoadedOrChanged;
 note.Unloaded -= NoteOnUnloaded;
 pin.PinMiddleMouseDown -= Pin_PinMiddleMouseDown;
 _pinsCanvas.Children.Remove(pin);
 _noteToPin.Remove(note);
 note.DetachOverlayPin();
 }

 private void Pin_PinMiddleMouseDown(object sender, MouseButtonEventArgs e)
 {
 if (sender is PinControl pin)
 {
 PinMiddleDragRequested?.Invoke(pin);
 e.Handled = true;
 }
 }

 public PinControl? GetPin(BaseNoteControl note) { _noteToPin.TryGetValue(note, out var pin); return pin; }
 public IEnumerable<PinControl> EnumeratePins() => _noteToPin.Values;

 private void NoteOnLoadedOrChanged(object? sender, EventArgs e)
 {
 if (sender is BaseNoteControl note && _noteToPin.TryGetValue(note, out var pin))
 {
 if (!SuppressDuringResize) EnforcePinExclusionFor(note);
 UpdatePinPosition(note, pin);
 }
 }

 private void NoteOnUnloaded(object? sender, RoutedEventArgs e) { if (sender is BaseNoteControl note) Unregister(note); }

 private void UpdatePinPosition(BaseNoteControl note, PinControl pin)
 {
 double left = Canvas.GetLeft(note); double top = Canvas.GetTop(note);
 if (double.IsNaN(left)) left =0; if (double.IsNaN(top)) top =0;
 double width = note.Width >0 ? note.Width : (note.ActualWidth >0 ? note.ActualWidth :0);
 double pinLeft = left + (width /2.0) - (pin.Width /2.0) + PinHorizontalOffset;
 double pinTop = top - (pin.Height /2.0) + PinVerticalOffset;
 Canvas.SetLeft(pin, pinLeft); Canvas.SetTop(pin, pinTop);
 }

 private Rect GetPinExclusionRectOnNotesCanvas(PinControl pin)
 {
 var tl = pin.TransformToVisual(_notesCanvas).Transform(new Point(0,0));
 var br = pin.TransformToVisual(_notesCanvas).Transform(new Point(pin.ActualWidth, pin.ActualHeight));
 var rect = new Rect(tl, br);
 rect.Inflate(PinExclusionPadding, PinExclusionPadding);
 return rect;
 }

 public IEnumerable<Rect> GetOtherPinsExclusionRectsOnNotesCanvas(BaseNoteControl movingNote)
 {
 return _noteToPin.Values.Where(p => p.OwnerNote != movingNote).Select(GetPinExclusionRectOnNotesCanvas).ToList();
 }

 private bool EnforcePinExclusionFor(BaseNoteControl movingNote)
 {
 if (_resolvingOverlap) return false; _resolvingOverlap = true; bool anyMoved = false;
 try
 {
 double left = Canvas.GetLeft(movingNote); double top = Canvas.GetTop(movingNote);
 if (double.IsNaN(left)) left =0; if (double.IsNaN(top)) top =0;
 double width = movingNote.Width >0 ? movingNote.Width : (movingNote.ActualWidth >0 ? movingNote.ActualWidth :0);
 double height = movingNote.Height >0 ? movingNote.Height : (movingNote.ActualHeight >0 ? movingNote.ActualHeight :0);
 if (width <=0 || height <=0) return false;
 var noteRect = new Rect(new Point(left, top), new Size(width, height));
 var pinRects = _noteToPin.Values.Where(p => p.OwnerNote != movingNote).Select(GetPinExclusionRectOnNotesCanvas).ToList();
 const int maxIterations =16; int iterations =0;
 while (iterations++ < maxIterations)
 {
 Rect? offender = null;
 for (int i =0; i < pinRects.Count; i++) if (noteRect.IntersectsWith(pinRects[i])) { offender = pinRects[i]; break; }
 if (offender == null) break;
 var others = pinRects.Where(r => r != offender.Value).ToList();
 var (dx, dy) = ChooseBestPush(noteRect, offender.Value, others);
 if (dx ==0 && dy ==0) break;
 left += dx; top += dy; noteRect = TranslateRect(noteRect, dx, dy); anyMoved = true;
 }
 if (anyMoved) { Canvas.SetLeft(movingNote, left); Canvas.SetTop(movingNote, top); }
 return anyMoved;
 }
 finally { _resolvingOverlap = false; }
 }

 private static (double dx, double dy) ChooseBestPush(Rect noteRect, Rect offender, IReadOnlyList<Rect> otherRects)
 {
 double pushLeft = -(noteRect.Right - offender.Left);
 double pushRight = (offender.Right - noteRect.Left);
 double pushUp = -(noteRect.Bottom - offender.Top);
 double pushDown = (offender.Bottom - noteRect.Top);
 var candidates = new (double dx, double dy)[] { (pushLeft,0), (pushRight,0), (0,pushUp), (0,pushDown) };
 (double dx, double dy)? best = null; double bestPenalty = double.PositiveInfinity; double bestMagnitude = double.PositiveInfinity;
 foreach (var (dx, dy) in candidates.OrderBy(c => Math.Abs(c.dx) + Math.Abs(c.dy)))
 {
 var moved = TranslateRect(noteRect, dx, dy); double penalty =0;
 for (int i =0; i < otherRects.Count; i++) { penalty += IntersectionArea(moved, otherRects[i]); if (penalty >0 && bestPenalty ==0) break; }
 double mag = Math.Abs(dx) + Math.Abs(dy);
 if (penalty < bestPenalty - double.Epsilon || (Math.Abs(penalty - bestPenalty) < double.Epsilon && mag < bestMagnitude))
 { best = (dx, dy); bestPenalty = penalty; bestMagnitude = mag; if (bestPenalty ==0) break; }
 }
 return best ?? (0,0);
 }
 private static Rect TranslateRect(Rect rect, double dx, double dy) => new Rect(new Point(rect.X + dx, rect.Y + dy), rect.Size);
 private static double IntersectionArea(Rect a, Rect b)
 {
 var i = Rect.Intersect(a, b);
 if (i.IsEmpty || i.Width <=0 || i.Height <=0) return 0;
 return i.Width * i.Height;
 }
 }
}
