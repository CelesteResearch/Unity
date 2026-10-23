using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Celeste.Unity.Content;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Celeste.Unity.Editor
{
    public static class WebPlayerBuild
    {
        public const string OutputDirectory = "Builds/Web";
        public const string ReportPath = "Builds/Web/report.json";
        public const string ManifestRelativePath = "Content/manifest.json";
        public const string FmodRuntimeRelativeDirectory = "Content/FMOD/2.03.15";
        private const string GameScene = "Assets/Scenes/Game.unity";
        private static bool scheduled;

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static string WorkspaceRoot => Path.GetFullPath(Path.Combine(ProjectRoot, ".."));

        private static string PowerShellExecutable =>
            Application.platform == RuntimePlatform.WindowsEditor ? "pwsh.exe" : "pwsh";

        public static string OutputPath => Path.Combine(WorkspaceRoot, OutputDirectory);

        public static void Schedule()
        {
            if (scheduled || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("A player build is already queued or running.");
            WriteReport(null, null, "Queued");
            scheduled = true;
            EditorApplication.delayCall += RunScheduled;
        }

        private static void RunScheduled()
        {
            scheduled = false;
            try
            {
                Build();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }

        [MenuItem("Celeste/Build Web Player")]
        public static void BuildMenu() => Schedule();

        public static string Build()
        {
            BuildReport report = null;
            string stage = "Preflight";
            try
            {
                if (scheduled || BuildPipeline.isBuildingPlayer)
                    throw new InvalidOperationException("A player build is already queued or running.");
                if (!File.Exists(Path.Combine(ProjectRoot, GameScene)))
                    throw new FileNotFoundException("The game scene is missing.", GameScene);
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                    throw new NotSupportedException("The installed Unity editor has no Web build support.");
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                    throw new InvalidOperationException(
                        "Activate the Web platform and finish its script reload before building; this entry does not change the active build target.");
                if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.WebGL) != ScriptingImplementation.IL2CPP)
                    throw new InvalidOperationException("The Web build target requires IL2CPP.");
                if (PlayerSettings.WebGL.template != "PROJECT:Celeste")
                    throw new InvalidOperationException("Select the Celeste Web template before building.");
                ValidateTouchArtwork(Path.Combine(ProjectRoot, "Assets/WebGLTemplates/Celeste/touch"));
                Directory.CreateDirectory(OutputPath);
                stage = "BuildPlayer";
                WriteReport(null, null, stage);
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { GameScene },
                    locationPathName = OutputPath,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.DetailedBuildReport
                });
                if (report.summary.result != BuildResult.Succeeded)
                {
                    WriteReport(report, null, stage);
                    return report.summary.result.ToString();
                }

                stage = "Il2CppResources";
                string[] dataArchives = Directory.GetFiles(Path.Combine(OutputPath, "Build"), "*.data.gz");
                if (dataArchives.Length != 1)
                    throw new InvalidDataException("The Web build must contain one gzip data archive.");
                WebDataArchive.IncludeGeneratedResources(dataArchives[0],
                    Path.Combine(ProjectRoot, "Library/Bee/artifacts/WebGL/il2cppOutput/data/Resources"));
                stage = "ContentManifest";
                GenerateManifest();
                stage = "PublishContent";
                PublishContent();
                stage = "FmodRuntime";
                PublishFmodRuntime(ProjectRoot, OutputPath);
                stage = "TemplateValidation";
                string index = Path.Combine(OutputPath, "index.html");
                ValidateGeneratedIndex(File.ReadAllText(index));
                ValidateTouchArtwork(Path.Combine(OutputPath, "touch"));
                WriteReport(report, null, "Complete");
                return report.summary.result.ToString();
            }
            catch (Exception error)
            {
                WriteReport(report, error, stage);
                throw;
            }
        }

        internal static void ValidateTouchArtwork(string directory)
        {
            const string restore =
                "Run workspace Tools/AssetRestore/Restore-Assets.ps1 -CelesteDirectory <game-directory> -RepositoryRoot <workspace-directory> -IncludeRuntimeContent.";
            ReadOnlySpan<byte> signature = stackalloc byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
            foreach (string name in new[] { "up", "down", "left", "right", "jump", "dash", "grab", "pause" })
            {
                string path = Path.Combine(directory, name + ".png");
                if (!File.Exists(path))
                    throw new FileNotFoundException("Web touch artwork is missing. " + restore, path);
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.Length < signature.Length || !bytes.AsSpan(0, signature.Length).SequenceEqual(signature))
                    throw new InvalidDataException("Web touch artwork is not a PNG: " + path + ". " + restore);
            }
        }

        internal static void ValidateGeneratedIndex(string html)
        {
            foreach (string marker in new[]
                     {
                         "<meta name=\"celeste-web-template\" content=\"1\"",
                         "autoSyncPersistentDataPath: false,"
                     })
            {
                int position = html.IndexOf(marker, StringComparison.Ordinal);
                if (position < 0 || html.IndexOf(marker, position + marker.Length, StringComparison.Ordinal) >= 0)
                    throw new InvalidDataException("The generated Celeste template has no unique contract marker: " +
                                                   marker);
            }
        }

        internal static void PublishFmodRuntime(string projectRoot, string outputPath)
        {
            string sourceDirectory = Path.Combine(projectRoot, FmodRuntimeRelativeDirectory);
            string destinationDirectory = Path.Combine(outputPath, FmodRuntimeRelativeDirectory);
            string[] files =
            {
                "fmodstudio.js", "fmodstudio.wasm", "fmodstudio.worker.js", "ThreadedFmodOutput.Worklet.js",
                "FmodBankRangeWorker.js"
            };
            foreach (string file in files)
            {
                string source = Path.Combine(sourceDirectory, file);
                if (!File.Exists(source))
                    throw new FileNotFoundException("The official FMOD HTML5 runtime file is missing.", source);
            }

            Directory.CreateDirectory(destinationDirectory);
            foreach (string file in files)
                File.Copy(Path.Combine(sourceDirectory, file), Path.Combine(destinationDirectory, file), true);
            File.Copy(Path.Combine(projectRoot, "Tools/Content/Web/VerifiedContentCache.js"),
                Path.Combine(destinationDirectory, "VerifiedContentCache.js"), true);
            foreach (string file in new[] { "FmodBankPageSource.js", "OriginalBankPages.json" })
                File.Copy(Path.Combine(projectRoot, "Tools/Audio/Fmod/Html5", file),
                    Path.Combine(destinationDirectory, file), true);
            string cacheDirectory = Path.Combine(outputPath, "Content/Cache");
            Directory.CreateDirectory(cacheDirectory);
            File.Copy(Path.Combine(projectRoot, "Tools/Content/Web/FullContentDownload.Worker.js"),
                Path.Combine(cacheDirectory, "FullContentDownload.Worker.js"), true);
        }

        private static void PublishContent()
        {
            var start = new ProcessStartInfo(PowerShellExecutable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = ProjectRoot
            };
            foreach (string argument in new[]
                     {
                         "-NoProfile", "-File", Path.Combine(ProjectRoot, "Tools/Content/Publish-WebContent.ps1"),
                         "-ContentRoot", UnityGameContentPaths.Current.Game,
                         "-FrameworkTimeRoot", UnityGameContentPaths.Current.FrameworkTimeResources,
                         "-ManifestPath", Path.Combine(OutputPath, ManifestRelativePath), "-OutputRoot", OutputPath
                     })
                start.ArgumentList.Add(argument);
            using Process process = Process.Start(start) ??
                                    throw new IOException("Unable to start Web content publication.");
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            Task.WaitAll(output, error);
            if (process.ExitCode != 0)
                throw new IOException(
                    $"Web content publication failed ({process.ExitCode}): {error.Result}{output.Result}");
            Debug.Log("[WebPlayerBuild] " + output.Result.Trim());
        }

        private static void GenerateManifest()
        {
            string tool = Path.Combine(ProjectRoot, "Tools/Content/Build-ContentManifest.ps1");
            string groups = Path.Combine(ProjectRoot, "Tools/Content/OriginalContentGroups.json");
            var start = new ProcessStartInfo(PowerShellExecutable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = ProjectRoot
            };
            foreach (string argument in new[]
                     {
                         "-NoProfile", "-File", tool, "-ContentRoot", UnityGameContentPaths.Current.Game,
                         "-GroupsPath", groups, "-OutputPath", Path.Combine(OutputPath, ManifestRelativePath)
                     })
                start.ArgumentList.Add(argument);
            using Process process = Process.Start(start) ??
                                    throw new IOException("Unable to start the content manifest generator.");
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            Task.WaitAll(output, error);
            if (process.ExitCode != 0)
                throw new IOException(
                    $"Content manifest generation failed ({process.ExitCode}): {error.Result}{output.Result}");
            Debug.Log("[WebPlayerBuild] " + output.Result.Trim());
        }

        private static void WriteReport(BuildReport report, Exception error, string stage)
        {
            var summary = report == null ? default : report.summary;
            var diagnostics = new
            {
                recordedUtc = DateTime.UtcNow.ToString("o"),
                stage,
                result = error != null ? "Exception" : report == null ? stage : summary.result.ToString(),
                buildResult = report == null ? null : summary.result.ToString(),
                postBuildComplete = stage == "Complete" && error == null,
                platform = report == null ? null : summary.platform.ToString(),
                buildGuid = report == null ? null : summary.guid.ToString(),
                buildStartedUtc = report == null ? null : summary.buildStartedAt.ToString("o"),
                buildEndedUtc = report == null ? null : summary.buildEndedAt.ToString("o"),
                totalBytes = report == null ? 0UL : summary.totalSize,
                totalErrors = report == null ? 0 : summary.totalErrors,
                totalWarnings = report == null ? 0 : summary.totalWarnings,
                seconds = report == null ? 0 : summary.totalTime.TotalSeconds,
                outputPath = report == null ? OutputPath : summary.outputPath,
                scenes = new[] { GameScene },
                manifestPath = Path.Combine(OutputPath, ManifestRelativePath),
                fmodRuntimeDirectory = Path.Combine(OutputPath, FmodRuntimeRelativeDirectory),
                autoSyncPersistentDataPath = stage == "Complete" && error == null ? (bool?)false : null,
                error = error?.ToString(),
                steps = report?.steps.Select(step => new
                {
                    step.name,
                    seconds = step.duration.TotalSeconds,
                    messages = step.messages.Select(message => new { type = message.type.ToString(), message.content })
                        .ToArray()
                }).ToArray()
            };
            string path = Path.Combine(WorkspaceRoot, ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path,
                JsonSerializer.Serialize(diagnostics, new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(false));
            Debug.Log($"[WebPlayerBuild] {diagnostics.result} ({stage}) -> {OutputPath}");
        }
    }
}
