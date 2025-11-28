using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VirtualCorkboard.Controls;
using VirtualCorkboard.Twine;
using VirtualCorkboard.Free.Controls;
using VirtualCorkboard.Serialization;
using VirtualCorkboard.Persistence.Models;
using VirtualCorkboard.Free.Serialization;
using VirtualCorkboard.Packaging;
using System.Windows.Threading;
using System.Diagnostics;
using System.Linq;
using VirtualCorkboard.Services;

namespace VirtualCorkboard
{
    public partial class MainWindow : Window, ISelectionHost, IWorkspaceDirtyState
    {
        private bool _isActive = true;
        private readonly TwineManager _twineManager;
        private readonly PinOverlayManager _pinOverlayManager;

        // New: Service-based architecture
        private readonly WorkspaceController _workspaceController;
        private readonly AutosaveService _autosaveService;
        private readonly InputController _inputController;
        private readonly TwineInteractionService _twineInteraction;
        private readonly NoteInteractionService _noteInteraction;

        // IWorkspaceDirtyState implementation for autosave
        public bool IsDirty => _workspaceController.IsDirty;
        public bool SuppressDirtyForNewWorkspace
        {
            get => _workspaceController.SuppressDirtyForNewWorkspace;
            set => _workspaceController.SuppressDirtyForNewWorkspace = value;
        }

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

            // Initialize core managers
            _twineManager = new TwineManager(TwineCanvas);
            _pinOverlayManager = new PinOverlayManager(PinsCanvas, NotesCanvas);

            // Initialize services
            var snapshotProvider = new WorkspaceSnapshotProvider(NotesCanvas, _twineManager);
            var uiBuilder = new WorkspaceUiBuilder(NotesCanvas, _twineManager, _pinOverlayManager);
            var package = new VcbPackage();
            var serializer = new WorkspaceSerializer();

            _workspaceController = new WorkspaceController(package, serializer, snapshotProvider, uiBuilder);
            _autosaveService = new AutosaveService(this, package, serializer, snapshotProvider, TimeSpan.FromSeconds(30));
            _twineInteraction = new TwineInteractionService(TwineCanvas, _twineManager, _pinOverlayManager, this);
            _inputController = new InputController(NotesCanvas, TwineCanvas, _twineManager, _twineInteraction);
            _noteInteraction = new NoteInteractionService(NotesCanvas, _twineManager, _pinOverlayManager);

            // Wire up service events
            _workspaceController.WorkspaceStateChanged += WorkspaceController_StateChanged;
            _workspaceController.TwineEndpointRefreshRequested += ScheduleTwineEndpointRefresh;

            _inputController.SaveRequested += () => _workspaceController.PerformSave(false);
            _inputController.ToggleActiveInactiveRequested += ToggleActiveInactive;
            _inputController.DirtyStateChanged += () => _workspaceController.MarkDirty();

            _twineInteraction.TwineConnectionCompleted += () => _workspaceController.MarkDirty();
            _noteInteraction.NoteModified += () => _workspaceController.MarkDirty();

            // Wire up sidebar events
            Sidebar.AddTextNoteRequested += Sidebar_AddTextNoteRequested;
            Sidebar.NewWorkspaceRequested += (_, __) => _workspaceController.NewWorkspace();
            Sidebar.OpenWorkspaceRequested += (_, __) => _workspaceController.PerformOpen();
            Sidebar.SaveWorkspaceRequested += (_, __) => _workspaceController.PerformSave(false);
            Sidebar.SaveAsWorkspaceRequested += (_, __) => _workspaceController.PerformSave(true);

            // Wire up input events to controller
            this.KeyDown += (s, e) => _inputController.HandleKeyDown(e);
            this.PreviewKeyDown += (s, e) => _inputController.HandlePreviewKeyDown(e);
            this.PreviewMouseUp += (s, e) => _inputController.HandlePreviewMouseUp(e);
            this.MouseMove += (s, e) => _inputController.HandleMouseMove(e);
            TwineCanvas.MouseLeftButtonDown += (s, e) => _inputController.HandleTwineCanvasMouseLeftButtonDown(e);
            TwineCanvas.KeyDown += (s, e) => _inputController.HandleTwineCanvasKeyDown(e);
            NotesCanvas.MouseLeftButtonDown += (s, e) => _inputController.HandleNotesCanvasMouseLeftButtonDown(e);

            // Wire up twine and pin events
            _pinOverlayManager.PinMiddleDragRequested += pin => _twineInteraction.StartTwineDragFromPin(pin);
            _twineManager.TwineSelectionChanged += TwineManager_TwineSelectionChanged;
            TwineCanvas.Focusable = true;

            // Window lifecycle
            this.Closing += MainWindow_Closing;

            // Initialize storage and attempt recovery
            StoragePaths.EnsureFolders();
            _autosaveService.SetRecoveryPathProvider(() => _workspaceController.GetRecoveryFileForCurrentWorkspace());
            _autosaveService.Start();
            _workspaceController.TryOfferRecovery();

            // Subscribe to global note events for dirty tracking
            BaseNoteControl.NoteDeleted += _ => _workspaceController.MarkDirty();
            TextNoteControl.TextEdited += _ =>
            {
                if (_workspaceController.SuppressDirtyForNewWorkspace)
                    _workspaceController.SuppressDirtyForNewWorkspace = false;
                _workspaceController.MarkDirty();
            };
            BaseNoteControl.NoteGeometryChanged += n =>
            {
                if (_workspaceController.SuppressDirtyForNewWorkspace)
                    _workspaceController.SuppressDirtyForNewWorkspace = false;
                _workspaceController.MarkDirty();
            };
        }

        private void WorkspaceController_StateChanged(object? sender, WorkspaceStateChangedEventArgs e)
        {
            UpdateTitle(e.CurrentFilePath, e.IsDirty);
            Sidebar.UpdateWorkspaceTitle(e.CurrentFilePath, e.IsDirty, e.IsRecoveredWorkspace);
        }

        private void UpdateTitle(string? filePath, bool isDirty)
        {
            var fileName = filePath != null ? System.IO.Path.GetFileName(filePath) : "(unsaved)";
            this.Title = isDirty ? $"VirtualCorkboard - {fileName} *" : $"VirtualCorkboard - {fileName}";
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_workspaceController.ConfirmDiscardIfDirty())
            {
                e.Cancel = true;
                return;
            }

            _autosaveService.Stop();
            _workspaceController.CleanupRecoveryOnExit();
        }

        private void ScheduleTwineEndpointRefresh()
        {
            // Defer until layout pass completes so pins have proper size/transform
            Dispatcher.InvokeAsync(() =>
            {
                foreach (var pin in _pinOverlayManager.EnumeratePins().ToList())
                {
                    pin.InvalidatePosition();
                    _twineManager.UpdateAllConnectionsForPin(pin);
                }
                _twineManager.RefreshAllConnections();
            }, DispatcherPriority.Loaded);
        }

        private void Sidebar_AddTextNoteRequested(object? sender, System.EventArgs e)
        {
            var note = _noteInteraction.CreateNote<TextNoteControl>(NoteKind.Text);
            note.Width = 210;
            note.Height = 160;
            note.NoteText = "New Note";

            if (_workspaceController.SuppressDirtyForNewWorkspace)
                _workspaceController.SuppressDirtyForNewWorkspace = false;
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

        private void ToggleSidebarButton_Click(object sender, RoutedEventArgs e)
        {
            Sidebar.Visibility = Sidebar.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
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
