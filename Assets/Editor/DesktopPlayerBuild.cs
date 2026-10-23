using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Celeste.Unity.Editor.Build;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Celeste.Unity.Editor
{
    public static class DesktopPlayerBuild
    {
        private const string ProductFileName = "Celeste in Unity";
        internal static string WorkspaceRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        [MenuItem("Celeste/Build Linux 64 Player")]
        public static void BuildLinuxMenu() => BuildLinux();

        public static string BuildLinux() => Build(BuildTarget.StandaloneLinux64);

        [MenuItem("Celeste/Build macOS 64 Player")]
        public static void BuildMacMenu() => BuildMac();

        public static string BuildMac()
        {
            DesktopNativeBuildProcessor.SelectMacX64();
            return Build(BuildTarget.StandaloneOSX);
        }

        [MenuItem("Celeste/Build Host Player")]
        public static void BuildHostMenu() => BuildHost();

        public static string BuildHost() => Application.platform switch
        {
            RuntimePlatform.WindowsEditor => PlayerBuild.Build(),
            RuntimePlatform.LinuxEditor => BuildLinux(),
            RuntimePlatform.OSXEditor => BuildMac(),
            _ => throw new PlatformNotSupportedException("A Windows, Linux or macOS desktop editor is required.")
        };

        private static string Build(BuildTarget target)
        {
            string runtime = DesktopNativeBuildProcessor.RuntimeIdentifier(target);
            string directory = Path.Combine(WorkspaceRoot, PlayerBuild.OutputDirectory, runtime);
            string output = Path.Combine(directory,
                ProductFileName + (target == BuildTarget.StandaloneOSX ? ".app" : ".x86_64"));
            BuildReport report = null;
            try
            {
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                    throw new BuildFailedException("Install the Unity desktop build support module for " + target +
                                                   ".");
                GraphicsDeviceType graphics = target == BuildTarget.StandaloneLinux64
                    ? GraphicsDeviceType.Vulkan
                    : GraphicsDeviceType.Metal;
                if (PlayerSettings.GetUseDefaultGraphicsAPIs(target) ||
                    !PlayerSettings.GetGraphicsAPIs(target).SequenceEqual(new[] { graphics }))
                {
                    PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
                    PlayerSettings.SetGraphicsAPIs(target, new[] { graphics });
                }

                string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path)
                    .ToArray();
                if (scenes.Length == 0) throw new BuildFailedException("EditorBuildSettings has no enabled scenes.");
                Directory.CreateDirectory(directory);
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = output,
                    target = target,
                    options = BuildOptions.None
                });
                if (report.summary.result == BuildResult.Succeeded)
                    WriteLaunchers(directory, target, output);
                WriteReport(directory, runtime, output, report, null);
                return report.summary.result.ToString();
            }
            catch (Exception error)
            {
                WriteReport(directory, runtime, output, report, error);
                throw;
            }
        }

        internal static string MacExecutable(string appPath)
        {
            var document = XDocument.Load(Path.Combine(appPath, "Contents", "Info.plist"));
            var key = document.Root.Element("dict").Elements("key")
                .Single(element => element.Value == "CFBundleExecutable");
            string name = key.ElementsAfterSelf().First().Value;
            if (name.Length == 0 || name != Path.GetFileName(name))
                throw new BuildFailedException("The macOS bundle executable name is invalid.");
            string executable = Path.Combine(appPath, "Contents", "MacOS", name);
            if (!File.Exists(executable))
                throw new BuildFailedException("The macOS bundle executable is missing: " + executable);
            return executable;
        }

        internal static void WriteLaunchers(string directory, BuildTarget target, string output)
        {
            string executable = target == BuildTarget.StandaloneOSX ? MacExecutable(output) : output;
            string relative = executable
                .Substring(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar).Length + 1)
                .Replace('\\', '/');
            string graphics = target == BuildTarget.StandaloneOSX ? "-force-metal" : "-force-vulkan";
            foreach (string profile in new[] { "Original", "Everest" })
            {
                string path = Path.Combine(directory, "Start-" + profile + ".sh");
                File.WriteAllText(path, LauncherText(relative, graphics, profile), new UTF8Encoding(false));
                MakeExecutable(path);
            }
        }

        internal static string LauncherText(string relative, string graphics, string profile) =>
            "#!/bin/sh\nset -eu\nDIR=$(CDPATH= cd -- \"$(dirname -- \"$0\")\" && pwd)\n" +
            "exec \"$DIR/" + relative.Replace("$", "\\$").Replace("`", "\\`").Replace("\"", "\\\"") + "\" " + graphics +
            " --" + profile.ToLowerInvariant() + " \"$@\"\n";

        internal static void MakeExecutable(string path)
        {
            if (Application.platform == RuntimePlatform.WindowsEditor) return;
            if (chmod(path, 0x1ed) != 0)
                throw new IOException("Cannot set executable permissions for " + path + ": " +
                                      Marshal.GetLastWin32Error());
        }

        private static void WriteReport(string directory, string runtime, string output, BuildReport report,
            Exception error)
        {
            Directory.CreateDirectory(directory);
            string executable = error == null && report != null && report.summary.result == BuildResult.Succeeded
                ? runtime == "osx-x64" ? MacExecutable(output) : output
                : null;
            File.WriteAllText(Path.Combine(directory, "report.json"), JsonSerializer.Serialize(new
            {
                finishedUtc = DateTime.UtcNow,
                result = error != null ? "Exception" : report.summary.result.ToString(),
                runtimeIdentifier = runtime,
                outputPath = output,
                executableSha256 = executable != null ? Sha256(executable) : null,
                totalErrors = report?.summary.totalErrors,
                totalWarnings = report?.summary.totalWarnings,
                error = error?.ToString()
            }, new JsonSerializerOptions { WriteIndented = true }));
        }

        internal static string Sha256(string path)
        {
            using var input = File.OpenRead(path);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
        }

        [DllImport("libc", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        private static extern int chmod([MarshalAs(UnmanagedType.LPUTF8Str)] string path, uint mode);
    }
}
