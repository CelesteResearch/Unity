using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace Celeste.Unity.Editor
{
    [Serializable]
    internal sealed class SharedAssemblyPublicationPlan
    {
#pragma warning disable CS0649 // JsonUtility populates the serialized publication fields.
        public int Schema;
        public string UnityRoot;
        public string PluginsPath;
        public FileEntry[] Files;
        public AssemblyEntry[] ManagedAssemblies;
#pragma warning restore CS0649

        public void ValidateForEditor(string projectRoot)
        {
            if (Schema != 1 || !SamePath(UnityRoot, projectRoot)
                            || !SamePath(PluginsPath,
                                Path.Combine(projectRoot, "Packages/local.celeste.shared/Runtime/Plugins")))
                throw new InvalidOperationException("The publication plan does not belong to this Unity project.");
            var existing = Directory.GetFiles(PluginsPath, "*.dll", SearchOption.AllDirectories);
            if (existing.Length != ManagedAssemblies.Length)
                throw new InvalidOperationException(
                    "The shared assembly set changed. Close this Editor before publishing the new identities.");
            foreach (var assembly in ManagedAssemblies)
            {
                if (!SamePath(Path.GetDirectoryName(assembly.Destination), PluginsPath)
                    || !File.Exists(assembly.Destination)
                    || AssemblyName.GetAssemblyName(assembly.Destination).FullName != assembly.Identity)
                    throw new InvalidOperationException(
                        "A shared assembly identity changed. Close this Editor before publication: " +
                        assembly.Destination);
            }

            var packageRoot = Path.GetFullPath(Path.Combine(projectRoot, "Packages/local.celeste.shared")) +
                              Path.DirectorySeparatorChar;
            var native = Path.GetFullPath(Path.Combine(projectRoot, "Assets/Runtime/Plugins/x86_64/lua54.dll"));
            foreach (var file in Files)
            {
                var destination = Path.GetFullPath(file.Destination);
                if (!destination.StartsWith(packageRoot, StringComparison.OrdinalIgnoreCase)
                    && !SamePath(destination, native) && !SamePath(destination, native + ".meta"))
                    throw new InvalidOperationException("The publication destination is outside the shared package: " +
                                                        destination);
                if (Hash(file.Source) != file.SHA256 ||
                    (File.Exists(destination) ? Hash(destination) : null) != file.PreviousSHA256)
                    throw new InvalidOperationException("Publication bytes changed after preparation: " + destination);
                if (SamePath(destination, native) && (!File.Exists(destination) || file.SHA256 != file.PreviousSHA256))
                    throw new InvalidOperationException(
                        "The native Lua library changed. Close this Editor before publication.");
            }
        }

        public void ApplyFiles()
        {
            foreach (var file in Files)
            {
                if (file.SHA256 == file.PreviousSHA256) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(file.Destination));
                File.Copy(file.Source, file.Destination, true);
                if (Hash(file.Destination) != file.SHA256)
                    throw new IOException("Published bytes differ: " + file.Destination);
            }
        }

        private static bool SamePath(string first, string second)
        {
            return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
        }

        private static string Hash(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        [Serializable]
        public sealed class FileEntry
        {
#pragma warning disable CS0649 // JsonUtility populates the serialized publication fields.
            public string Source;
            public string Destination;
            public string SHA256;
            public string PreviousSHA256;
#pragma warning restore CS0649
        }

        [Serializable]
        public sealed class AssemblyEntry
        {
#pragma warning disable CS0649 // JsonUtility populates the serialized publication fields.
            public string Destination;
            public string Identity;
#pragma warning restore CS0649
        }
    }
}
