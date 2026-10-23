using UnityEditor;
using UnityEngine;

public static class ProjectBootstrap
{
    public static void ImportOdin()
    {
        if (System.IO.File.Exists(System.IO.Path.Combine(Application.dataPath,
                "Plugins/Sirenix/Assemblies/Sirenix.OdinInspector.Editor.dll")))
        {
            Debug.Log("Odin is already installed.");
            if (Application.isBatchMode) EditorApplication.Exit(0);
            return;
        }

        string package = System.Environment.GetEnvironmentVariable("CELESTE_ODIN_PACKAGE");
        string[] arguments = System.Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < arguments.Length; i++)
            if (arguments[i] == "-odinPackage")
                package = arguments[i + 1];
        if (string.IsNullOrWhiteSpace(package) && !Application.isBatchMode)
            package = EditorUtility.OpenFilePanel("Import licensed Odin package", "", "unitypackage");
        if (string.IsNullOrWhiteSpace(package) || !System.IO.File.Exists(package))
            throw new System.IO.FileNotFoundException("Select your licensed Odin package or pass -odinPackage.",
                package);

        SessionState.SetBool("Unity.ImportingOdin", true);
        AssetDatabase.importPackageCompleted += _ =>
        {
            Debug.Log("Odin package import complete.");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        };
        AssetDatabase.importPackageFailed += (_, reason) =>
        {
            Debug.LogError(reason);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        };
        UnityEditor.AssetPackage.Package.Import(System.IO.Path.GetFullPath(package), false);
        EditorSettings.serializationMode = SerializationMode.ForceText;
        VersionControlSettings.mode = "Visible Meta Files";
        global::UnityEditor.PlayerSettings.companyName = "OLC";
        global::UnityEditor.PlayerSettings.productName = "Celeste in Unity";
        global::UnityEditor.PlayerSettings.defaultScreenWidth = 1280;
        global::UnityEditor.PlayerSettings.defaultScreenHeight = 720;
        Debug.Log("Unity project settings configured; Odin import requested.");
    }

    [InitializeOnLoadMethod]
    private static void ResumePackageImport()
    {
        if (!SessionState.GetBool("Unity.ImportingOdin", false)) return;
        EditorApplication.update += FinishImport;
    }

    private static void FinishImport()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!System.IO.File.Exists(System.IO.Path.Combine(Application.dataPath,
                "Plugins/Sirenix/Odin Inspector/Version.txt"))) return;
        SessionState.EraseBool("Unity.ImportingOdin");
        EditorApplication.update -= FinishImport;
        Debug.Log("Odin import and assembly reload completed.");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
