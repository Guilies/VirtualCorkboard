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
            var manifestEntry = zip.GetEntry(Persistence.PersistenceConstants.ManifestFileName) ?? throw new InvalidDataException("manifest.json missing");
            using var reader = new StreamReader(manifestEntry.Open());
            var json = reader.ReadToEnd();

            return serializer.Deserialize(json);
        }
    }
}
