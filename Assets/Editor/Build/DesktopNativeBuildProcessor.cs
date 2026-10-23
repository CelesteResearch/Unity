using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Celeste.Unity.Editor.Build
{
    public sealed partial class DesktopNativeBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private sealed class Library
        {
            internal string Name;
            internal string Source;
            internal string Sha256;
        }

        public int callbackOrder => 1000;

        internal static string RuntimeIdentifier(BuildTarget target) => target switch
        {
            BuildTarget.StandaloneLinux64 => "linux-x64",
            BuildTarget.StandaloneOSX => "osx-x64",
            _ => throw new ArgumentOutOfRangeException(nameof(target))
        };

        private static bool IsPortableDesktop(BuildTarget target) =>
            target == BuildTarget.StandaloneLinux64 || target == BuildTarget.StandaloneOSX;

        private static string AdapterName(BuildTarget target) => target == BuildTarget.StandaloneOSX
            ? "libGfxPluginCelesteGraphicsUnityMetal.dylib"
            : "libGfxPluginCelesteGraphicsUnityVulkan.so";

        private static bool IsNativeLibrary(string name, BuildTarget target) => target == BuildTarget.StandaloneOSX
            ? name.EndsWith(".dylib", StringComparison.Ordinal)
            : name.EndsWith(".so", StringComparison.Ordinal) || name.Contains(".so.");

        private static Library[] CollectLibraries(BuildTarget target)
        {
            string directory = Path.Combine(DesktopPlayerBuild.WorkspaceRoot, "artifacts", "runtime",
                RuntimeIdentifier(target));
            return CollectLibraries(target, directory);
        }

        private static Library[] CollectLibraries(BuildTarget target, string directory)
        {
            string runtime = RuntimeIdentifier(target);
            if (!Directory.Exists(directory))
                throw new BuildFailedException("Prepare the native runtime before building: " + directory);
            var libraries = Directory.GetFiles(directory).Where(path => IsNativeLibrary(Path.GetFileName(path), target))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => new Library
                    { Name = Path.GetFileName(path), Source = path, Sha256 = DesktopPlayerBuild.Sha256(path) })
                .ToArray();
            var names = new HashSet<string>(libraries.Select(library => library.Name), StringComparer.Ordinal);
            string[] required = target == BuildTarget.StandaloneLinux64
                ? new[]
                {
                    "libSDL2.so", "libSDL2-2.0.so.0", "libFAudio.so", "libFAudio.so.0", "libFNA3D.so", "libFNA3D.so.0",
                    "libtheorafile.so", "libfmod.so", "libfmod.so.10", "libfmodstudio.so", "libfmodstudio.so.10",
                    "libfmod_SDL.so", "liblua54.so", "libCelesteGraphicsVulkan.so", "libCelesteGraphicsEffects.so",
                    "libCelesteUnityPlatform.so", AdapterName(target)
                }
                : new[]
                {
                    "libSDL2.dylib", "libFAudio.dylib", "libFNA3D.dylib", "libtheorafile.dylib", "libfmod.dylib",
                    "libfmodstudio.dylib", "liblua54.dylib", "libMoltenVK.dylib", "libCelesteGraphicsVulkan.dylib",
                    "libCelesteGraphicsEffects.dylib", "libCelesteUnityPlatform.dylib", AdapterName(target)
                };
            foreach (string name in required)
                if (!names.Contains(name))
                    throw new BuildFailedException("The prepared " + runtime + " native runtime is missing " + name +
                                                   ".");
            foreach (string name in target == BuildTarget.StandaloneLinux64
                         ? new[] { "libfmod.so.10", "libfmodstudio.so.10" }
                         : Array.Empty<string>())
            {
                if (libraries.Single(library => library.Name == name).Sha256 != libraries
                        .Single(library => library.Name == name.Substring(0, name.Length - 3)).Sha256)
                    throw new BuildFailedException("The prepared FMOD SONAME alias differs: " + name);
            }

            return libraries;
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            BuildTarget target = report.summary.platform;
            if (!IsPortableDesktop(target)) return;
            RequireNativeBuildHost(target);
            if (target == BuildTarget.StandaloneOSX) RequireMacX64();
            var libraries = CollectLibraries(target);
            string plugin = Path.Combine(Application.dataPath, "Runtime", "Plugins", RuntimeIdentifier(target),
                AdapterName(target));
            if (!File.Exists(plugin) || DesktopPlayerBuild.Sha256(plugin) !=
                libraries.Single(library => library.Name == AdapterName(target)).Sha256)
                throw new BuildFailedException(
                    "Unity's imported graphics adapter differs from the prepared native runtime; run Tools/Prepare.ps1 again.");
        }

        private static void RequireNativeBuildHost(BuildTarget target)
        {
            RuntimePlatform host = target == BuildTarget.StandaloneOSX
                ? RuntimePlatform.OSXEditor
                : RuntimePlatform.LinuxEditor;
            if (Application.platform != host)
                throw new BuildFailedException(
                    "Finalize this desktop player on its matching native editor host so executable permissions, native aliases and signing are preserved.");
            if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
                throw new BuildFailedException(
                    "The FMOD 1.10.14 desktop runtime requires an x64 editor; use the Intel editor through Rosetta on Apple Silicon.");
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            BuildTarget target = report.summary.platform;
            if (!IsPortableDesktop(target)) return;
            string output = Path.GetFullPath(report.summary.outputPath);
            string directory = PluginDirectory(report, output, target);
            Library[] libraries = CollectLibraries(target);
            var deployed = new List<(Library Primary, string[] Aliases)>();
            var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var group in libraries.GroupBy(library => library.Sha256, StringComparer.Ordinal))
            {
                Library primary = group.OrderByDescending(library => library.Name.Length)
                    .ThenBy(library => library.Name, StringComparer.Ordinal).First();
                string destination = Path.Combine(directory, primary.Name);
                File.Delete(destination);
                File.Copy(primary.Source, destination);
                if (DesktopPlayerBuild.Sha256(destination) != primary.Sha256)
                    throw new BuildFailedException("The deployed native library differs: " + primary.Name);
                DesktopPlayerBuild.MakeExecutable(destination);
                foreach (Library library in group) aliases.Add(library.Name, primary.Name);
                foreach (Library alias in group.Where(library => library != primary))
                {
                    string aliasPath = Path.Combine(directory, alias.Name);
                    File.Delete(aliasPath);
                    if (symlink(primary.Name, aliasPath) != 0)
                        throw new IOException("Cannot preserve native library alias " + alias.Name + ": " +
                                              Marshal.GetLastWin32Error());
                }

                deployed.Add((primary,
                    group.Where(library => library != primary).Select(library => library.Name).ToArray()));
            }

            if (target == BuildTarget.StandaloneOSX)
            {
                DesktopPlayerBuild.MakeExecutable(DesktopPlayerBuild.MacExecutable(output));
                RelocateAndSignMac(output, directory, aliases);
            }
            else DesktopPlayerBuild.MakeExecutable(output);

            string manifest = Path.Combine(Path.GetDirectoryName(output), "native-closure.json");
            File.WriteAllText(manifest,
                JsonSerializer.Serialize(
                    new
                    {
                        runtimeIdentifier = RuntimeIdentifier(target), pluginDirectory = directory,
                        libraries = deployed.Select(library => new
                        {
                            file = library.Primary.Name, sourceSha256 = library.Primary.Sha256,
                            deployedSha256 = DesktopPlayerBuild.Sha256(Path.Combine(directory, library.Primary.Name)),
                            aliases = library.Aliases
                        }).ToArray()
                    }, new JsonSerializerOptions { WriteIndented = true }));
        }

        private static string PluginDirectory(BuildReport report, string output, BuildTarget target)
        {
            return PluginDirectory(report.GetFiles().Select(file => file.path), output, target);
        }

        private static string PluginDirectory(IEnumerable<string> paths, string output, BuildTarget target)
        {
            string adapter = AdapterName(target);
            string[] directories = paths.Where(path => Path.GetFileName(path) == adapter && File.Exists(path))
                .Select(path => Path.GetFullPath(Path.GetDirectoryName(path))).Distinct(StringComparer.Ordinal)
                .ToArray();
            if (directories.Length != 1)
                throw new BuildFailedException(
                    "The build must contain one imported native graphics adapter: " + adapter);
            string expectedRoot = target == BuildTarget.StandaloneOSX
                ? Path.Combine(output, "Contents", "PlugIns")
                : Path.Combine(Path.GetDirectoryName(output), Path.GetFileNameWithoutExtension(output) + "_Data",
                    "Plugins");
            string directory = directories[0];
            string expected = Path.GetFullPath(expectedRoot).TrimEnd(Path.DirectorySeparatorChar);
            StringComparison comparison = target == BuildTarget.StandaloneOSX
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!directory.Equals(expected, comparison) &&
                !directory.StartsWith(expected + Path.DirectorySeparatorChar, comparison))
                throw new BuildFailedException(
                    "Unity placed the native adapter outside the player's plugin directory: " + directory);
            return directory;
        }

        private static Type MacSettingsType =>
            Type.GetType("UnityEditor.OSXStandalone.UserBuildSettings, UnityEditor.OSXStandalone.Extensions", true);

        internal static void SelectMacX64()
        {
            RequireNativeBuildHost(BuildTarget.StandaloneOSX);
            PropertyInfo architecture =
                MacSettingsType.GetProperty("architecture", BindingFlags.Public | BindingFlags.Static);
            architecture.SetValue(null, Enum.Parse(architecture.PropertyType, "x64"));
        }

        private static void RequireMacX64()
        {
            if (MacSettingsType.GetProperty("architecture", BindingFlags.Public | BindingFlags.Static).GetValue(null)
                    .ToString() != "x64")
                throw new BuildFailedException(
                    "Select Intel x64 for the macOS player; the FMOD 1.10.14 runtime does not contain ARM64 code.");
        }

        [DllImport("libc", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        private static extern int symlink([MarshalAs(UnmanagedType.LPUTF8Str)] string target,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string linkPath);
    }
}
