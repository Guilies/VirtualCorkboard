using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VirtualCorkboard.Commands;
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

        // Track active transform commands
        private Free.Commands.MoveNoteCommand? _activeMoveCommand;

        // New: Service-based architecture
        private readonly WorkspaceController _workspaceController;
        private readonly AutosaveService _autosaveService;
        private readonly Services.CommandManager _commandManager;
        private readonly InputController _inputController;
        private readonly TwineInteractionService _twineInteraction;
        private readonly NoteInteractionService _noteInteraction;
        private readonly WorkspaceViewportService _viewportService;

        // IWorkspaceDirtyState implementation for autosave
        public bool IsDirty => _workspaceController.IsDirty;
        public bool SuppressDirtyForNewWorkspace
        {
            get => _workspaceController.SuppressDirtyForNewWorkspace;
            set => _workspaceController.SuppressDirtyForNewWorkspace = value;
        }

        public void MarkDirty()
        {
            _workspaceController.MarkDirty();
        }

        // Expose key elements/managers for shells
        public Canvas NotesCanvasElement => NotesCanvas;
        public Canvas PinsCanvasElement => PinsCanvas;
        public Canvas TwineCanvasElement => TwineCanvas;
        public PinOverlayManager PinOverlayManager => _pinOverlayManager;
        public TwineManager TwineManager => _twineManager;
        public Controls.SidebarControl SidebarControl => Sidebar;
        public Services.CommandManager CommandManager => _commandManager;

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
            _commandManager = new Services.CommandManager(this);
            _twineInteraction = new TwineInteractionService(TwineCanvas, _twineManager, _pinOverlayManager, this);
            _inputController = new InputController(NotesCanvas, TwineCanvas, _twineManager, _twineInteraction, _commandManager);
            _noteInteraction = new NoteInteractionService(NotesCanvas, _twineManager, _pinOverlayManager);
            _viewportService = new WorkspaceViewportService(WorkspaceContainer, WorkspaceContainer, WorkspaceScrollViewer);

            // Connect viewport service to snapshot/builder after it's created
            snapshotProvider.SetViewportService(_viewportService);
            uiBuilder.SetViewportService(_viewportService, WorkspaceContainer);

            // Wire up service events
            _workspaceController.WorkspaceStateChanged += WorkspaceController_StateChanged;
            _workspaceController.TwineEndpointRefreshRequested += ScheduleTwineEndpointRefresh;

            _inputController.SaveRequested += () => _workspaceController.PerformSave(false);
            _inputController.ToggleActiveInactiveRequested += ToggleActiveInactive;
            _inputController.DirtyStateChanged += () => _workspaceController.MarkDirty();

            _twineInteraction.TwineConnectionCompleted += () => _workspaceController.MarkDirty();
            _noteInteraction.NoteModified += () => _workspaceController.MarkDirty();

            // Wire up viewport events for dirty tracking
            _viewportService.ZoomChanged += (s, zoom) => _workspaceController.MarkDirty();
            _viewportService.PanChanged += (s, pan) => _workspaceController.MarkDirty();
            
            // Wire up zoom indicator update
            _viewportService.ZoomChanged += (s, zoom) => UpdateZoomIndicator(zoom);

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
            this.PreviewMouseWheel += (s, e) => _viewportService.HandleMouseWheel(e);
            WorkspaceScrollViewer.PreviewMouseDown += (s, e) => _viewportService.HandleMouseDown(e);
            WorkspaceScrollViewer.PreviewMouseUp += (s, e) => _viewportService.HandleMouseUp(e);
            WorkspaceScrollViewer.PreviewMouseMove += (s, e) => _viewportService.HandleMouseMove(e);
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
            
            // Initialize zoom indicator
            UpdateZoomIndicator(_viewportService.ZoomLevel);

            // Subscribe to global note events for dirty tracking
            BaseNoteControl.NoteDeleted += _ => _workspaceController.MarkDirty();
            BaseNoteControl.NoteDeletionRequested += HandleNoteDeletionRequested;
            BaseNoteControl.NoteMoveStarted += HandleNoteMoveStarted;
            BaseNoteControl.NoteMoveCompleted += HandleNoteMoveCompleted;
            BaseNoteControl.NoteResizeCompleted += HandleNoteResizeCompleted;
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

        private void UpdateZoomIndicator(double zoomLevel)
        {
            // Update zoom indicator text with percentage
            int zoomPercent = (int)Math.Round(zoomLevel * 100);
            ZoomLevelText.Text = $"{zoomPercent}%";
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
            // Create command to add text note
            var command = new Free.Commands.AddNoteCommand(
                _noteInteraction,
                _pinOverlayManager,
                _twineManager,
                NotesCanvas,
                typeof(Free.Controls.TextNoteControl),
                NoteKind.Text,
                new Point(100, 100), // Default position
                new Size(210, 160)   // Default size
            );

            _commandManager.Execute(command);

            // Set properties on the created note
            if (command.CreatedNote is Free.Controls.TextNoteControl textNote)
            {
                textNote.NoteText = "New Note";
            }

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

        private void HandleNoteDeletionRequested(System.Collections.Generic.List<BaseNoteControl> toRemove)
        {
            if (toRemove == null || toRemove.Count == 0)
                return;

            // Create individual removal commands for each note
            var removalCommands = toRemove.Select(note =>
            {
                return (Commands.ICommand)new Free.Commands.RemoveNoteCommand(
                    _pinOverlayManager,
                    _twineManager,
                    NotesCanvas,
                    note.NoteId);
            }).ToList();

            // Wrap in composite if multiple notes, otherwise use single command
            Commands.ICommand finalCommand;
            if (removalCommands.Count == 1)
            {
                finalCommand = removalCommands[0];
            }
            else
            {
                finalCommand = new Commands.CompositeCommand(
                    removalCommands,
                    $"Delete {removalCommands.Count} Notes"
                );
            }

            // Execute through command manager
            _commandManager.Execute(finalCommand);
        }

        private void HandleNoteMoveStarted(System.Collections.Generic.List<BaseNoteControl> notes)
        {
            if (notes == null || notes.Count == 0)
                return;

            // Create move command with starting positions
            _activeMoveCommand = new Free.Commands.MoveNoteCommand(NotesCanvas, notes);
        }

        private void HandleNoteMoveCompleted(System.Collections.Generic.List<BaseNoteControl> notes)
        {
            if (_activeMoveCommand == null || notes == null || notes.Count == 0)
                return;

            // Update final positions
            _activeMoveCommand.UpdateFinalPositions(notes);

            // Only create command if notes actually moved
            if (_activeMoveCommand.HasMoved())
            {
                _commandManager.Execute(_activeMoveCommand);
            }

            _activeMoveCommand = null;
        }

        private void HandleNoteResizeCompleted(BaseNoteControl note, Rect oldBounds, Rect newBounds)
        {
            if (note == null)
                return;

            // Create and execute resize command
            var command = new Free.Commands.ResizeNoteCommand(
                NotesCanvas,
                note.NoteId,
                oldBounds,
                newBounds);

            _commandManager.Execute(command);
        }
    }
}

