using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using VirtualCorkboard.Packaging;
using VirtualCorkboard.Persistence.Models;
using VirtualCorkboard.Serialization;

namespace VirtualCorkboard.Services
{
    /// <summary>
    /// Manages workspace file operations: New, Open, Save, Save As, and Recovery.
    /// Does NOT touch UI directly - only coordinates data operations and raises events.
    /// </summary>
    public class WorkspaceController
    {
        private readonly IVcbPackage _package;
        private readonly IWorkspaceSerializer _serializer;
        private readonly IWorkspaceSnapshotSource _snapshotProvider;
        private readonly IWorkspaceBuilder _uiBuilder;

        private string? _currentFilePath;
        private bool _isRecoveredWorkspace;
        private WorkspaceModel? _currentModel;

        // State management
        public bool IsDirty { get; private set; }
        public bool SuppressDirtyForNewWorkspace { get; set; }

        public string? CurrentFilePath => _currentFilePath;
        public bool IsRecoveredWorkspace => _isRecoveredWorkspace;

        // Events to notify UI of state changes
        public event EventHandler<WorkspaceStateChangedEventArgs>? WorkspaceStateChanged;
        public event Action<string?>? TitleUpdateRequested;
        public event Action? TwineEndpointRefreshRequested;

        public WorkspaceController(
            IVcbPackage package,
            IWorkspaceSerializer serializer,
            IWorkspaceSnapshotSource snapshotProvider,
            IWorkspaceBuilder uiBuilder)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
            _uiBuilder = uiBuilder ?? throw new ArgumentNullException(nameof(uiBuilder));
        }

        public void MarkDirty()
        {
            if (SuppressDirtyForNewWorkspace) return;
            IsDirty = true;
            RaiseStateChanged();
        }

        public void ClearDirty()
        {
            IsDirty = false;
            RaiseStateChanged();
        }

        public void NewWorkspace()
        {
            if (!ConfirmDiscardIfDirty()) return;

            _uiBuilder.ClearWorkspace();
            _currentModel = null;
            _currentFilePath = null;
            _isRecoveredWorkspace = false;
            SuppressDirtyForNewWorkspace = true;
            ClearDirty();
        }

        public void PerformOpen()
        {
            if (!ConfirmDiscardIfDirty()) return;

            var ofd = new OpenFileDialog
            {
                Filter = "Virtual Corkboard (*.vcb)|*.vcb",
                DefaultExt = "vcb"
            };

            if (ofd.ShowDialog() != true) return;

            try
            {
                var model = _package.Load(ofd.FileName, _serializer);
                _currentFilePath = ofd.FileName;
                _isRecoveredWorkspace = false;
                StoragePaths.WriteLastWorkspacePath(_currentFilePath);

                _uiBuilder.ClearWorkspace();
                foreach (var note in model.Notes) _uiBuilder.CreateNote(note);
                _uiBuilder.FinalizeNotes();
                foreach (var conn in model.Connections) _uiBuilder.CreateConnection(conn);
                _uiBuilder.ApplySettings(model.Settings);

                _currentModel = model;
                ClearDirty();
                TwineEndpointRefreshRequested?.Invoke();
            }
            catch (InvalidDataException ex)
            {
                MessageBox.Show($"Cannot open workspace:\n\n{ex.Message}", "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An unexpected error occurred while loading the workspace:\n\n{ex.Message}", "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"[WorkspaceController.PerformOpen] Exception: {ex}");
            }
        }

        public bool PerformSave(bool saveAs)
        {
            // Recovery workspace or no path -> force Save As
            if (_currentFilePath == null || saveAs || _isRecoveredWorkspace)
            {
                var sfd = new SaveFileDialog
                {
                    Filter = "Virtual Corkboard (*.vcb)|*.vcb",
                    DefaultExt = "vcb",
                    InitialDirectory = Directory.Exists(StoragePaths.UserSavesFolder)
                        ? StoragePaths.UserSavesFolder
                        : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };

                if (sfd.ShowDialog() != true) return false;

                var chosenPath = sfd.FileName;

                // Block saving inside recovery folder
                if (IsPathInsideRecoveryFolder(chosenPath))
                {
                    MessageBox.Show(
                        "You cannot save workspace files inside the Recovery folder. Please choose a different location.",
                        "Invalid Save Location",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }

                _currentFilePath = chosenPath;
                _isRecoveredWorkspace = false;
            }

            try
            {
                var model = new WorkspaceModel
                {
                    Notes = _snapshotProvider.CaptureNotes().ToList(),
                    Connections = _snapshotProvider.CaptureConnections().ToList(),
                    Settings = _snapshotProvider.CaptureSettings()
                };

                _package.Save(_currentFilePath!, model, _serializer);
                StoragePaths.WriteLastWorkspacePath(_currentFilePath);

                // Clean up recovery file after successful save
                try
                {
                    var recovery = StoragePaths.GetWorkspaceRecoveryFile(_currentFilePath);
                    if (File.Exists(recovery)) File.Delete(recovery);
                }
                catch { }

                _currentModel = model;
                ClearDirty();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save workspace:\n\n{ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"[WorkspaceController.PerformSave] Exception: {ex}");
                return false;
            }
        }

        public void TryOfferRecovery()
        {
            try
            {
                var lastPath = StoragePaths.ReadLastWorkspacePath();
                var recoveryFile = StoragePaths.GetWorkspaceRecoveryFile(lastPath);

                if (!File.Exists(recoveryFile)) return;

                var label = Path.GetFileNameWithoutExtension(lastPath ?? "Unsaved Workspace");
                var result = MessageBox.Show(
                    $"A recovery file was found for '{label}'. Restore it?",
                    "Workspace Recovery",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;

                try
                {
                    var model = _package.Load(recoveryFile, _serializer);

                    _uiBuilder.ClearWorkspace();
                    foreach (var note in model.Notes) _uiBuilder.CreateNote(note);
                    _uiBuilder.FinalizeNotes();
                    foreach (var conn in model.Connections) _uiBuilder.CreateConnection(conn);
                    _uiBuilder.ApplySettings(model.Settings);

                    _currentModel = model;
                    _currentFilePath = null; // recovery = unsaved; require Save As
                    _isRecoveredWorkspace = true;
                    MarkDirty();
                    TwineEndpointRefreshRequested?.Invoke();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to restore recovery file:\n\n{ex.Message}\n\nThe recovery file may be corrupted.",
                        "Recovery Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    Debug.WriteLine($"[WorkspaceController.TryOfferRecovery] Exception: {ex}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WorkspaceController.TryOfferRecovery] Failed to check recovery: {ex}");
            }
        }

        public bool ConfirmDiscardIfDirty()
        {
            if (!IsDirty) return true;

            var result = MessageBox.Show(
                "Unsaved changes. Save first?",
                "Confirm",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Cancel) return false;
            if (result == MessageBoxResult.Yes)
            {
                if (!PerformSave(false)) return false;
            }

            return true;
        }

        public string GetRecoveryFileForCurrentWorkspace()
        {
            var dir = StoragePaths.GetWorkspaceRecoveryDirectory(_currentFilePath);
            try { Directory.CreateDirectory(dir); } catch { }
            return StoragePaths.GetWorkspaceRecoveryFile(_currentFilePath);
        }

        public void CleanupRecoveryOnExit()
        {
            if (IsDirty) return; // Don't delete if there are unsaved changes

            try
            {
                var lastPath = StoragePaths.ReadLastWorkspacePath();
                var recovery = StoragePaths.GetWorkspaceRecoveryFile(lastPath);
                if (File.Exists(recovery)) File.Delete(recovery);
            }
            catch { }
        }

        private bool IsPathInsideRecoveryFolder(string path)
        {
            try
            {
                var fullPath = Path.GetFullPath(path);
                var recoveryRoot = Path.GetFullPath(StoragePaths.RecoveryFolder);
                return fullPath.StartsWith(recoveryRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private void RaiseStateChanged()
        {
            WorkspaceStateChanged?.Invoke(this, new WorkspaceStateChangedEventArgs
            {
                CurrentFilePath = _currentFilePath,
                IsDirty = IsDirty,
                IsRecoveredWorkspace = _isRecoveredWorkspace
            });
        }
    }

    public class WorkspaceStateChangedEventArgs : EventArgs
    {
        public string? CurrentFilePath { get; init; }
        public bool IsDirty { get; init; }
        public bool IsRecoveredWorkspace { get; init; }
    }
}
