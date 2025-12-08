using System;
using System.IO;
using System.IO.Compression;
using VirtualCorkboard.Persistence.Models;
using VirtualCorkboard.Serialization;

namespace VirtualCorkboard.Packaging
{
    public class VcbPackage : IVcbPackage
    {
        public void Save(string path, WorkspaceModel model, IWorkspaceSerializer serializer)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Invalid path", nameof(path));
            if (model == null) throw new ArgumentNullException(nameof(model));

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (File.Exists(path)) File.Delete(path);
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
            var json = serializer.Serialize(model);
            var entry = zip.CreateEntry(Persistence.PersistenceConstants.ManifestFileName, CompressionLevel.Optimal);

            using (var writer = new StreamWriter(entry.Open()))
            {
                writer.Write(json);
            }
            // Reserve media folder (empty for MVP)
            zip.CreateEntry(Persistence.PersistenceConstants.MediaFolderName + "/");
        }

        public WorkspaceModel Load(string path, IWorkspaceSerializer serializer)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Invalid path", nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("Package not found", path);

            using var zip = ZipFile.OpenRead(path);
            var manifestEntry = zip.GetEntry(Persistence.PersistenceConstants.ManifestFileName) ?? throw new InvalidDataException("Workspace file is corrupted: manifest.json missing");
            using var reader = new StreamReader(manifestEntry.Open());
            var json = reader.ReadToEnd();

            var model = serializer.Deserialize(json);

            // Validate version
            if (string.IsNullOrWhiteSpace(model.Version))
                throw new InvalidDataException("Workspace file is missing version information");

            if (!IsVersionCompatible(model.Version))
                throw new InvalidDataException($"Incompatible workspace version.\nFile version: {model.Version}\nSupported version: {Persistence.PersistenceConstants.SaveVersion}");

            // Migrate older versions to current version
            model = MigrateToCurrentVersion(model);

            return model;
        }

        private static bool IsVersionCompatible(string fileVersion)
        {
            // Support version 1.0 and 1.1 (migration will handle differences)
            return fileVersion == "1.0" || fileVersion == Persistence.PersistenceConstants.SaveVersion;
        }

        private static WorkspaceModel MigrateToCurrentVersion(WorkspaceModel model)
        {
            if (model.Version == "1.0")
            {
                // Migrate from 1.0 to 1.1
                // Version 1.0 didn't have WorkspaceWidth/WorkspaceHeight in settings
                // Apply defaults if they're not set or are zero
                if (model.Settings != null)
                {
                    if (model.Settings.WorkspaceWidth <= 0)
                        model.Settings.WorkspaceWidth = 5000.0;
                    
                    if (model.Settings.WorkspaceHeight <= 0)
                        model.Settings.WorkspaceHeight = 5000.0;
                }

                // Update version to current
                model.Version = Persistence.PersistenceConstants.SaveVersion;
            }

            return model;
        }
    }
}
