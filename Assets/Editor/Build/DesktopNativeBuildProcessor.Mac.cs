using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor.Build;

namespace Celeste.Unity.Editor.Build
{
    public sealed partial class DesktopNativeBuildProcessor
    {
        private static void RelocateAndSignMac(string app, string directory, Dictionary<string, string> aliases)
        {
            string[] names = aliases.Values.Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal).ToArray();
            var identityToFile = new Dictionary<string, string>(StringComparer.Ordinal);
            var identities = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string name in names)
            {
                string path = Path.Combine(directory, name);
                string identity = RunMacTool("/usr/bin/otool", "-arch", "x86_64", "-D", path)
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Skip(1).Single().Trim();
                identityToFile.Add(identity, name);
                identities.Add(name, identity);
            }

            foreach (string name in names)
            {
                string path = Path.Combine(directory, name);
                string dependencies = RunMacTool("/usr/bin/otool", "-arch", "x86_64", "-L", path);
                foreach (string line in dependencies.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                             .Skip(1))
                {
                    string value = line.Trim();
                    int metadata = value.IndexOf(" (", StringComparison.Ordinal);
                    string dependency = metadata < 0 ? value : value.Substring(0, metadata);
                    if (dependency == identities[name]) continue;
                    if (identityToFile.TryGetValue(dependency, out string target) ||
                        aliases.TryGetValue(Path.GetFileName(dependency), out target))
                    {
                        string relocated = "@loader_path/" + target;
                        if (dependency != relocated)
                            RunMacTool("/usr/bin/install_name_tool", "-change", dependency, relocated, path);
                    }
                    else if (!dependency.StartsWith("/usr/lib/", StringComparison.Ordinal) &&
                             !dependency.StartsWith("/System/Library/", StringComparison.Ordinal))
                        throw new BuildFailedException("The macOS native runtime has an unresolved dependency: " +
                                                       dependency + " required by " + name);
                }

                RunMacTool("/usr/bin/install_name_tool", "-id", "@rpath/" + name, path);
            }

            // Unity signs the original build before postprocessing. Re-run its public signer
            // after the final native payload, retaining the project's signing configuration.
            Type signer =
                Type.GetType("UnityEditor.OSXStandalone.MacOSCodeSigning, UnityEditor.OSXStandalone.Extensions", true);
            MethodInfo method = signer.GetMethod("CodeSignAppBundle", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(string) }, null);
            method.Invoke(null, new object[] { app });
            RunMacTool("/usr/bin/codesign", "--verify", "--deep", "--strict", app);
        }

        private static string RunMacTool(string executable, params string[] arguments)
        {
            static string Quote(string argument) => "\"" + argument.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo(executable, string.Join(" ", arguments.Select(Quote)))
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new BuildFailedException(Path.GetFileName(executable) + " failed: " +
                                               error.GetAwaiter().GetResult());
            return output.GetAwaiter().GetResult();
        }
    }
}
