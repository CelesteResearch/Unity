using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Celeste.Unity.Editor
{
    /// <summary>Builds the Windows player and writes launchers and a build report.</summary>
    public static class PlayerBuild
    {
        public const string OutputDirectory = "Builds/Unity";
        public const string ExecutableName = "Celeste in Unity.exe";
        public const string ReportPath = "Builds/Unity/report.json";

        private static string WorkspaceRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        public static string OutputPath
        {
            get { return Path.Combine(WorkspaceRoot, OutputDirectory, ExecutableName); }
        }

        /// <summary>Queue the build for the next editor tick; returns immediately.</summary>
        public static void Schedule()
        {
            EditorApplication.delayCall += () =>
            {
                try
                {
                    Build();
                }
                catch (Exception error)
                {
                    WriteReport(null, error);
                }
            };
        }

        [MenuItem("Celeste/Build Windows 64 Player")]
        public static void BuildMenu() => Build();

        public static string Build()
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("EditorBuildSettings has no enabled scenes.");
            string output = OutputPath;
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var started = DateTime.UtcNow;
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
                WriteLaunchers(Path.GetDirectoryName(output));
            WriteReport(report, null, scenes, started);
            return report.summary.result.ToString();
        }

        internal static void WriteLaunchers(string directory)
        {
            void Write(string name, string arguments) => File.WriteAllText(Path.Combine(directory, name),
                $"@echo off\r\ncd /d \"%~dp0\"\r\nstart \"\" \"%~dp0{ExecutableName}\" {arguments} %*\r\n",
                Encoding.ASCII);

            Write("Start-Original.bat", "--original");
            Write("Start-Everest.bat", "--everest");
            foreach (string backend in new[] { "D3D11", "Vulkan", "D3D12" })
            {
                string argument = "-force-" + backend.ToLowerInvariant();
                Write($"Start-{backend}.bat", argument + " --original");
                Write($"Start-Everest-{backend}.bat", argument + " --everest");
            }
        }

        private static void WriteReport(BuildReport report, Exception error, string[] scenes = null,
            DateTime started = default)
        {
            var json = new StringBuilder();
            json.Append("{\n");
            json.Append("  \"finishedUtc\": \"").Append(DateTime.UtcNow.ToString("o")).Append("\",\n");
            if (report != null)
            {
                var summary = report.summary;
                json.Append("  \"result\": \"").Append(summary.result).Append("\",\n");
                json.Append("  \"platform\": \"").Append(summary.platform).Append("\",\n");
                json.Append("  \"totalBytes\": ").Append(summary.totalSize).Append(",\n");
                json.Append("  \"totalErrors\": ").Append(summary.totalErrors).Append(",\n");
                json.Append("  \"totalWarnings\": ").Append(summary.totalWarnings).Append(",\n");
                json.Append("  \"seconds\": ")
                    .Append(
                        (DateTime.UtcNow - (started == default ? DateTime.UtcNow : started)).TotalSeconds
                        .ToString("0.0")).Append(",\n");
                json.Append("  \"outputPath\": \"").Append(OutputPath.Replace("\\", "\\\\")).Append("\",\n");
                if (summary.result == BuildResult.Succeeded)
                {
                    string managed = Path.Combine(Path.GetDirectoryName(OutputPath),
                        Path.GetFileNameWithoutExtension(OutputPath) + "_Data", "Managed");
                    json.Append("  \"outputSha256\": \"").Append(Sha256(OutputPath)).Append("\",\n");
                    json.Append("  \"simulationSha256\": \"")
                        .Append(Sha256(Path.Combine(managed, "Celeste.Simulation.dll"))).Append("\",\n");
                    json.Append("  \"graphicsFormatSha256\": \"")
                        .Append(Sha256(Path.Combine(managed, "Celeste.Formats.Graphics.dll"))).Append("\",\n");
                    json.Append("  \"graphicsSha256\": \"")
                        .Append(Sha256(Path.Combine(managed, "Celeste.Graphics.dll"))).Append("\",\n");
                    json.Append("  \"gameRuntimeSha256\": \"")
                        .Append(Sha256(Path.Combine(managed, "Celeste.Game.Runtime.dll"))).Append("\",\n");
                }

                var steps = report.steps.Select(step => new { name = step.name, seconds = step.duration.TotalSeconds })
                    .ToArray();
                json.Append("  \"steps\": [");
                for (int i = 0; i < steps.Length; i++)
                {
                    json.Append("{ \"name\": \"").Append(steps[i].name).Append("\", \"seconds\": ")
                        .Append(steps[i].seconds.ToString("0.0")).Append("}");
                    if (i + 1 < steps.Length) json.Append(", ");
                }

                json.Append("],\n");
            }
            else
            {
                json.Append("  \"result\": \"Exception\",\n");
            }

            json.Append("  \"scenes\": [");
            if (scenes != null)
                for (int i = 0; i < scenes.Length; i++)
                {
                    json.Append('"').Append(scenes[i]).Append('"');
                    if (i + 1 < scenes.Length) json.Append(", ");
                }

            json.Append("],\n");
            if (error != null)
                json.Append("  \"error\": \"")
                    .Append(error.ToString().Replace("\\", "\\\\").Replace("\"", "'").Replace("\n", " "))
                    .Append("\",\n");
            json.Append("  \"exists\": ").Append(File.Exists(OutputPath) ? "true" : "false").Append("\n");
            json.Append("}\n");
            string path = Path.Combine(WorkspaceRoot, ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json.ToString());
            Debug.Log("[PlayerBuild] " + (report != null ? report.summary.result.ToString() : "exception") + " -> " +
                      OutputPath);
        }

        private static string Sha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
