using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VirtualCorkboard.Controls;
using VirtualCorkboard.Twine;
using VirtualCorkboard.Free.Controls;

namespace VirtualCorkboard
{
 public partial class MainWindow : Window, ISelectionHost
 {
 private bool _isActive = true;
 private TwineManager _twineManager;
 private PinControl? _twineDragSourcePin;
 private bool _isTwineDragging = false;
 private PinOverlayManager _pinOverlayManager;

 // Expose key elements/managers for shells
 public Canvas NotesCanvasElement => NotesCanvas;
 public Canvas PinsCanvasElement => PinsCanvas;
 public Canvas TwineCanvasElement => TwineCanvas;
 public PinOverlayManager PinOverlayManager => _pinOverlayManager;
 public TwineManager TwineManager => _twineManager;
 public Controls.SidebarControl SidebarControl => Sidebar;

 public MainWindow()
 {
 InitializeComponent();
 Sidebar.AddTextNoteRequested += Sidebar_AddTextNoteRequested;
 this.KeyDown += MainWindow_KeyDown;
 this.PreviewMouseUp += MainWindow_PreviewMouseUp;
 this.PreviewKeyDown += MainWindow_PreviewKeyDown;
 _twineManager = new TwineManager(TwineCanvas);
 _pinOverlayManager = new PinOverlayManager(PinsCanvas, NotesCanvas);
 _pinOverlayManager.PinMiddleDragRequested += pin => StartTwineDragFromPin(pin);
 _twineManager.TwineSelectionChanged += TwineManager_TwineSelectionChanged;
 TwineCanvas.MouseLeftButtonDown += TwineCanvas_MouseLeftButtonDown;
 TwineCanvas.Focusable = true;
 TwineCanvas.KeyDown += TwineCanvas_KeyDown;
 }

 // Allow shells to initiate twine drag from a pin
 public void StartTwineDragFromPin(PinControl pin)
 {
 if (_isTwineDragging) return;
 _twineDragSourcePin = pin;
 _isTwineDragging = true;
 var pos = pin.GetPinPositionOnWorkspace();
 _twineManager.StartTwineConnection(pin, pos);
 Mouse.Capture(this, CaptureMode.SubTree);
 }

 private void ToggleSidebarButton_Click(object sender, RoutedEventArgs e)
 {
 Sidebar.Visibility = Sidebar.Visibility == Visibility.Visible
 ? Visibility.Collapsed
 : Visibility.Visible;
 }

 private void Sidebar_AddTextNoteRequested(object? sender, System.EventArgs e)
 {
 // Create and add a new TextNote to the workspace canvas
 var note = new TextNoteControl
 {
 Width =210,
 Height =160,
 NoteText = "New Note",
 ClampToParentBounds = true
 };

 // Ensure the note participates in twine updates
 note.TwineManager = _twineManager;

 // Add to canvas first so layout metrics are available
 NotesCanvas.Children.Add(note);

 // Default placement: centered if possible, else fallback to (100,100)
 double canvasW = NotesCanvas.ActualWidth;
 double canvasH = NotesCanvas.ActualHeight;
 double left = double.IsNaN(canvasW) || canvasW <=0 ?100 : (canvasW - note.Width) /2.0;
 double top = double.IsNaN(canvasH) || canvasH <=0 ?100 : (canvasH - note.Height) /2.0;
 Canvas.SetLeft(note, left);
 Canvas.SetTop(note, top);

 // Register pin overlay for this note
 _pinOverlayManager.Register(note, NoteKind.Text);

 // Select and bring focus to the newly created note
 note.IsSelected = false;
 }

 private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
 {
 if (e.Key == Key.Delete)
 {
 if (TryDeleteSelectedTwine()) { e.Handled = true; return; }
 }
 if (e.Key == Key.F1) { ToggleActiveInactive(); }
 }

 private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
 {
 if (e.Key == Key.Delete)
 {
 if (!e.Handled && TryDeleteSelectedTwine()) { e.Handled = true; }
 }
 }

 private void TwineCanvas_KeyDown(object? sender, System.Windows.Input.KeyEventArgs e)
 {
 if (e.Key == Key.Delete)
 {
 if (TryDeleteSelectedTwine()) { e.Handled = true; }
 }
 }

 private bool TryDeleteSelectedTwine()
 {
 if (_twineManager != null && _twineManager.HasSelectedConnections)
 {
 _twineManager.DeleteSelectedConnections();
 return true;
 }
 return false;
 }

 private void ToggleActiveInactive()
 {
 if (_isActive)
 {
 DimmingOverlay.Visibility = Visibility.Collapsed;
 this.Hide();
 _isActive = false;
 }
 else
 {
 this.Show();
 DimmingOverlay.Visibility = Visibility.Visible;
 this.Topmost = true;
 this.Activate();
 _isActive = true;
 }
 }

 private void NotesCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
 {
 if (ReferenceEquals(e.Source, NotesCanvas))
 {
 foreach (var child in NotesCanvas.Children)
 if (child is BaseNoteControl note) note.IsSelected = false;
 _twineManager.ClearSelection();
 }
 }

 private void TwineCanvas_MouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
 {
 if (ReferenceEquals(e.Source, TwineCanvas))
 {
 foreach (var child in NotesCanvas.Children)
 if (child is BaseNoteControl note) note.IsSelected = false;
 }
 }

 private void MainWindow_PreviewMouseUp(object sender, MouseButtonEventArgs e)
 {
 if (e.ChangedButton != MouseButton.Middle) return;
 if (_isTwineDragging && _twineDragSourcePin != null)
 {
 var mousePos = e.GetPosition(this);
 PinControl? targetPin = FindClosestPin(mousePos,50);
 if (targetPin != null && targetPin != _twineDragSourcePin) _twineManager.CompleteConnection(targetPin);
 else _twineManager.CancelConnection();
 _isTwineDragging = false;
 _twineDragSourcePin = null;
 Mouse.Capture(null);
 e.Handled = true;
 }
 }

 protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
 {
 base.OnMouseMove(e);
 if (_isTwineDragging && _twineDragSourcePin != null)
 {
 var pos = e.GetPosition(TwineCanvas);
 _twineManager.UpdateGhostLine(pos);
 }
 }

 private PinControl? FindClosestPin(System.Windows.Point mousePos, double maxDistance)
 {
 PinControl? closestPin = null; double closestDist = maxDistance;
 foreach (var pin in _pinOverlayManager.EnumeratePins())
 {
 var pinPos = pin.TransformToAncestor(this).Transform(new System.Windows.Point(pin.ActualWidth /2, pin.ActualHeight /2));
 double dist = (pinPos - mousePos).Length;
 if (dist < closestDist) { closestDist = dist; closestPin = pin; }
 }
 return closestPin;
 }

 private void TwineManager_TwineSelectionChanged(object? sender, System.EventArgs e)
 {
 foreach (var child in NotesCanvas.Children)
 if (child is BaseNoteControl note) note.IsSelected = false;
 if (!TwineCanvas.IsFocused) TwineCanvas.Focus();
 }

 public void NoteSelected()
 {
 _twineManager.ClearSelection();
 if (!IsFocused) Focus();
 }
 }
}
