using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Celeste.Unity.Editor
{
    internal static class SharedAssemblySync
    {
        private static Process preparation;
        private static Task<string> output;
        private static Task<string> errors;

        private static bool IsIdle => !EditorApplication.isPlayingOrWillChangePlaymode
                                      && !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
                                      !BuildPipeline.isBuildingPlayer;

        [MenuItem("Celeste/Sync Shared Assemblies", true)]
        private static bool CanSync()
        {
            return preparation == null && IsIdle;
        }

        [MenuItem("Celeste/Sync Shared Assemblies")]
        private static void BeginSync()
        {
            if (!CanSync()) return;
            var script = Path.Combine(ProjectRoot, "Tools/Sync-SharedAssemblies.ps1");
            var start = new ProcessStartInfo
            {
                FileName = Application.platform == RuntimePlatform.WindowsEditor
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                        "PowerShell/7/pwsh.exe")
                    : "pwsh",
                Arguments = "-NoLogo -NoProfile -File " + Quote(script) + " -UnityRoot " + Quote(ProjectRoot) +
                            " -PrepareOnly",
                WorkingDirectory = ProjectRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            preparation = Process.Start(start);
            output = preparation.StandardOutput.ReadToEndAsync();
            errors = preparation.StandardError.ReadToEndAsync();
            EditorApplication.update += CompletePreparation;
        }

        private static void CompletePreparation()
        {
            if (!preparation.HasExited || !output.IsCompleted || !errors.IsCompleted) return;
            EditorApplication.update -= CompletePreparation;
            var process = preparation;
            preparation = null;
            try
            {
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("Shared assembly preparation failed: " +
                                                        errors.GetAwaiter().GetResult() +
                                                        output.GetAwaiter().GetResult());
                if (!IsIdle)
                    throw new InvalidOperationException(
                        "Publication is prepared, but this Editor is no longer idle. Run Sync Shared Assemblies again after stopping Play/compilation.");
                var planPath = Path.Combine(ProjectRoot, "Logs/SharedAssemblies/PortableAssemblyPublicationPlan.json");
                var plan = JsonUtility.FromJson<SharedAssemblyPublicationPlan>(File.ReadAllText(planPath));
                plan.ValidateForEditor(ProjectRoot);
                ApplyInEditor(plan);
                Debug.Log("Shared assemblies synchronized; Unity will reload the updated scripts.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                process.Dispose();
                output = null;
                errors = null;
            }
        }

        private static void ApplyInEditor(SharedAssemblyPublicationPlan plan)
        {
            var reloadLocked = false;
            var refreshDisabled = false;
            try
            {
                EditorApplication.LockReloadAssemblies();
                reloadLocked = true;
                AssetDatabase.DisallowAutoRefresh();
                refreshDisabled = true;
                plan.ApplyFiles();
            }
            finally
            {
                try
                {
                    if (refreshDisabled) AssetDatabase.AllowAutoRefresh();
                }
                finally
                {
                    try
                    {
                        if (reloadLocked) EditorApplication.UnlockReloadAssemblies();
                    }
                    finally
                    {
                        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    }
                }
            }
        }

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static string Quote(string argument)
        {
            return "\"" + argument.Replace("\"", "\\\"") + "\"";
        }
    }
}
