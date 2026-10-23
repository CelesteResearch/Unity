using Celeste.Hosting.Runtime.Content;
using Celeste.Unity.Content;
using UnityEditor;
using UnityEditor.Build;

namespace Celeste.Unity.Editor.Build
{
    public sealed class DesktopContentBuildProcessor : BuildPlayerProcessor
    {
        public override int callbackOrder => 0;

        public override void PrepareForBuild(BuildPlayerContext context)
        {
            var target = context.BuildPlayerOptions.target;
            if (target != BuildTarget.StandaloneWindows64 && target != BuildTarget.StandaloneOSX &&
                target != BuildTarget.StandaloneLinux64) return;

            var content = UnityGameContentPaths.Current;
            context.AddAdditionalPathToStreamingAssets(content.Game, GameContentPaths.GameDirectoryName);
            context.AddAdditionalPathToStreamingAssets(content.Everest, GameContentPaths.EverestDirectoryName);
            context.AddAdditionalPathToStreamingAssets(content.FrameworkTimeResources,
                GameContentPaths.FrameworkTimeResourcesDirectoryName);
        }
    }
}
