using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VirtualCorkboard.Controls;
using VirtualCorkboard.Twine;
using VirtualCorkboard.Free.Controls;
using VirtualCorkboard.Serialization;
using VirtualCorkboard.Persistence.Models;
using VirtualCorkboard.Free.Serialization;
using Microsoft.Win32;
using VirtualCorkboard.Packaging;

namespace VirtualCorkboard
{
     public partial class MainWindow : Window, ISelectionHost
     {
         private bool _isActive = true;
         private TwineManager _twineManager;
         private PinControl? _twineDragSourcePin;
         private bool _isTwineDragging = false;
         private PinOverlayManager _pinOverlayManager;

        // persistence helpers
         private WorkspaceSnapshotProvider? _snapshotProvider;
         private WorkspaceUiBuilder? _uiBuilder;
         private IWorkspaceSerializer _serializer = new WorkspaceSerializer();
         private WorkspaceModel? _currentModel;
         private readonly IVcbPackage _package = new VcbPackage();
         private string? _currentFilePath;
         private bool _isDirty;

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

             Sidebar.NewWorkspaceRequested += (_,__) => NewWorkspace();
             Sidebar.OpenWorkspaceRequested += (_,__) => PerformOpen();
             Sidebar.SaveWorkspaceRequested += (_,__) => PerformSave(false);
             Sidebar.SaveAsWorkspaceRequested += (_,__) => PerformSave(true);

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

             // init persistence helpers
             _snapshotProvider = new WorkspaceSnapshotProvider(NotesCanvas, _twineManager);
             _uiBuilder = new WorkspaceUiBuilder(NotesCanvas, _twineManager, _pinOverlayManager);
             this.Closing += MainWindow_Closing;
         }

         private void MarkDirty() { _isDirty = true; UpdateTitle(); }
         private void ClearDirty() { _isDirty = false; UpdateTitle(); }
         private void UpdateTitle()
         {
             var fileName = _currentFilePath != null ? System.IO.Path.GetFileName(_currentFilePath) : "(unsaved)";
             this.Title = _isDirty ? $"VirtualCorkboard - {fileName} *" : $"VirtualCorkboard - {fileName}";
         }

         private bool ConfirmDiscardIfDirty()
         {
             if (!_isDirty) return true;
             var result = MessageBox.Show("Unsaved changes. Save first?", "Confirm", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
             if (result == MessageBoxResult.Cancel) return false;
             if (result == MessageBoxResult.Yes)
             {
                if (!PerformSave(false)) return false; // if save cancelled
             }
             return true;
         }

         private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
         {
            if (!ConfirmDiscardIfDirty()) e.Cancel = true;
         }

         // File operations
         private void NewWorkspace()
         {
             if (!ConfirmDiscardIfDirty()) return;
             _uiBuilder?.ClearWorkspace();
             _currentModel = null;
             _currentFilePath = null;
             MarkDirty(); // new empty workspace considered dirty until first save
         }

         private bool PerformSave(bool saveAs)
         {
             if (_snapshotProvider == null) return false;
             if (_currentFilePath == null || saveAs)
             {
                 var sfd = new SaveFileDialog
                 {
                     Filter = "Virtual Corkboard (*.vcb)|*.vcb",
                     DefaultExt = "vcb"
                 };
                 if (sfd.ShowDialog() != true) return false;
                 _currentFilePath = sfd.FileName;
             }

             var model = new WorkspaceModel
             {
                 Notes = _snapshotProvider.CaptureNotes().ToList(),
                 Connections = _snapshotProvider.CaptureConnections().ToList(),
                 Settings = _snapshotProvider.CaptureSettings()
             };
             _package.Save(_currentFilePath!, model, _serializer);
             _currentModel = model;
             ClearDirty();
             return true;
         }

         private void PerformOpen()
         {
             if (!ConfirmDiscardIfDirty()) return;
             var ofd = new OpenFileDialog
             {
                 Filter = "Virtual Corkboard (*.vcb)|*.vcb",
                 DefaultExt = "vcb"
             };
             if (ofd.ShowDialog() != true) return;
             var model = _package.Load(ofd.FileName, _serializer);
             _currentFilePath = ofd.FileName;
             _uiBuilder?.ClearWorkspace();
             foreach (var note in model.Notes) _uiBuilder?.CreateNote(note);
             _uiBuilder?.FinalizeNotes();
             foreach (var conn in model.Connections) _uiBuilder?.CreateConnection(conn);
             _uiBuilder?.ApplySettings(model.Settings);
             _twineManager.RefreshAllConnections();
             _currentModel = model; 
             ClearDirty();
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
             MarkDirty();
         }

         private void ToggleSidebarButton_Click(object sender, RoutedEventArgs e)
         {
             Sidebar.Visibility = Sidebar.Visibility == Visibility.Visible
             ? Visibility.Collapsed
             : Visibility.Visible;
         }

         private void Sidebar_AddTextNoteRequested(object? sender, System.EventArgs e)
         {
             var note = new TextNoteControl
             {
                 Width =210,
                 Height =160,
                 NoteText = "New Note",
                 ClampToParentBounds = true
             };
             note.TwineManager = _twineManager;
             NotesCanvas.Children.Add(note);
             double canvasW = NotesCanvas.ActualWidth;
             double canvasH = NotesCanvas.ActualHeight;
             double left = double.IsNaN(canvasW) || canvasW <=0 ?100 : (canvasW - note.Width) /2.0;
             double top = double.IsNaN(canvasH) || canvasH <=0 ?100 : (canvasH - note.Height) /2.0;
             Canvas.SetLeft(note, left);
             Canvas.SetTop(note, top);
             _pinOverlayManager.Register(note, NoteKind.Text);
             note.IsSelected = false;
             MarkDirty();
         }

         private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
         {
            //Delete key to delete selected twine connections or notes
            if (e.Key == Key.Delete)
             {
                if (TryDeleteSelectedTwine()) { e.Handled = true; MarkDirty(); return; }
             }

             // Ctrl+F1 to toggle active/inactive
             if (e.Key == Key.F1) { ToggleActiveInactive(); }

             // Ctrl+S to save
             if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
             {
                PerformSave(false);
             }
         }

         private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
         {
             if (e.Key == Key.Delete)
             {
                if (!e.Handled && TryDeleteSelectedTwine()) { e.Handled = true; MarkDirty(); }
             }
         }

         private void TwineCanvas_KeyDown(object? sender, System.Windows.Input.KeyEventArgs e)
         {
             if (e.Key == Key.Delete)
             {
                if (TryDeleteSelectedTwine()) { e.Handled = true; MarkDirty(); }
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
                 MarkDirty();
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
