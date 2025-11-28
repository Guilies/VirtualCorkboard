using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using VirtualCorkboard.Packaging;
using VirtualCorkboard.Persistence.Models;
using VirtualCorkboard.Serialization;

namespace VirtualCorkboard.Services
{
    /// <summary>
    /// Handles automatic workspace saving at regular intervals.
    /// </summary>
    public class AutosaveService
    {
        private readonly DispatcherTimer _timer;
        private readonly IWorkspaceDirtyState _dirtyState;
        private readonly IVcbPackage _package;
        private readonly IWorkspaceSerializer _serializer;
        private readonly IWorkspaceSnapshotSource _snapshotProvider;
        private Func<string>? _getRecoveryPath;

        public AutosaveService(
            IWorkspaceDirtyState dirtyState,
            IVcbPackage package,
            IWorkspaceSerializer serializer,
            IWorkspaceSnapshotSource snapshotProvider,
            TimeSpan interval)
        {
            _dirtyState = dirtyState ?? throw new ArgumentNullException(nameof(dirtyState));
            _package = package ?? throw new ArgumentNullException(nameof(package));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));

            _timer = new DispatcherTimer { Interval = interval };
            _timer.Tick += OnTimerTick;
        }

        public void SetRecoveryPathProvider(Func<string> getRecoveryPath)
        {
            _getRecoveryPath = getRecoveryPath;
        }

        public void Start()
        {
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (!_dirtyState.IsDirty || _getRecoveryPath == null)
            {
                return;
            }

            try
            {
                var path = _getRecoveryPath();
                Debug.WriteLine($"[AutosaveService] Autosaving to: {path}");

                var model = new WorkspaceModel
                {
                    Notes = _snapshotProvider.CaptureNotes().ToList(),
                    Connections = _snapshotProvider.CaptureConnections().ToList(),
                    Settings = _snapshotProvider.CaptureSettings()
                };

                // Ensure directory exists
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                _package.Save(path, model, _serializer);
                Debug.WriteLine("[AutosaveService] Autosave completed successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AutosaveService] Autosave failed: {ex.Message}");
            }
        }
    }
}
