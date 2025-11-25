using System;
using System.IO;

namespace VirtualCorkboard.Packaging
{
    public static class StoragePaths
    {
        public static string AppDataRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VirtualCorkboard");

        public static string RecoveryFolder => Path.Combine(AppDataRoot, "Recovery");

        public static string UserSavesFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VirtualCorkboard", "Saves");

        public static string LastWorkspaceInfoFile => Path.Combine(AppDataRoot, "last_workspace.txt");

        public static void EnsureFolders()
        {
            Directory.CreateDirectory(AppDataRoot);
            Directory.CreateDirectory(RecoveryFolder);
            Directory.CreateDirectory(UserSavesFolder);
        }

        public static string GetWorkspaceRecoveryDirectory(string? workspacePath)
        {
            var baseName = GetWorkspaceBaseName(workspacePath);
            var dir = Path.Combine(RecoveryFolder, baseName + "_recovery");
            return dir;
        }

        public static string GetWorkspaceRecoveryFile(string? workspacePath)
        {
            var dir = GetWorkspaceRecoveryDirectory(workspacePath);
            var baseName = GetWorkspaceBaseName(workspacePath);
            return Path.Combine(dir, baseName + "_recovery.vcb");
        }

        private static string GetWorkspaceBaseName(string? workspacePath)
        {
            if (string.IsNullOrWhiteSpace(workspacePath)) return "unsaved";
            try
            {
                var name = Path.GetFileNameWithoutExtension(workspacePath);
                return string.IsNullOrWhiteSpace(name) ? "workspace" : Sanitize(name);
            }
            catch { return "workspace"; }
        }

        private static string Sanitize(string input)
        {
            // Remove characters invalid for folder/file names
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                input = input.Replace(c.ToString(), "_");
            }
            return input.Trim();
        }

        public static void WriteLastWorkspacePath(string? path)
        {
            try
            {
                File.WriteAllText(LastWorkspaceInfoFile, path ?? string.Empty);
            }
            catch { }
        }

        public static string? ReadLastWorkspacePath()
        {
            try
            {
                if (!File.Exists(LastWorkspaceInfoFile)) return null;
                var content = File.ReadAllText(LastWorkspaceInfoFile).Trim();
                return string.IsNullOrWhiteSpace(content) ? null : content;
            }
            catch { return null; }
        }
    }
}