using System;
using UnityEditor;

public sealed class NativePluginImport : AssetPostprocessor
{
    private void OnPreprocessAsset()
    {
        bool nativePath = assetPath.StartsWith("Assets/Plugins/FMOD/", StringComparison.Ordinal) ||
                          assetPath.StartsWith("Assets/Runtime/Plugins/", StringComparison.Ordinal);
        if (!nativePath || !(assetImporter is PluginImporter plugin)) return;
        string directory = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(assetPath));
        string runtime = directory == "x86_64" ? "win-x64" : directory;
        string os;
        string cpu;
        BuildTarget target;
        switch (runtime)
        {
            case "win-x64":
                os = "Windows";
                cpu = "x86_64";
                target = BuildTarget.StandaloneWindows64;
                break;
            case "linux-x64":
                os = "Linux";
                cpu = "x86_64";
                target = BuildTarget.StandaloneLinux64;
                break;
            case "osx-x64":
                os = "OSX";
                cpu = "x86_64";
                target = BuildTarget.StandaloneOSX;
                break;
            case "osx-arm64":
                os = "OSX";
                cpu = "ARM64";
                target = BuildTarget.StandaloneOSX;
                break;
            default: return;
        }

        plugin.SetCompatibleWithAnyPlatform(false);
        plugin.SetCompatibleWithEditor(true);
        plugin.SetEditorData("CPU", cpu);
        plugin.SetEditorData("OS", os);
        foreach (BuildTarget platform in new[]
                     { BuildTarget.StandaloneWindows64, BuildTarget.StandaloneLinux64, BuildTarget.StandaloneOSX })
            plugin.SetCompatibleWithPlatform(platform, platform == target);
        plugin.SetPlatformData(target, "CPU", cpu);
    }
}
