using System.IO;
using Celeste.Hosting.Runtime.Content;
using UnityEngine;

namespace Celeste.Unity.Content
{
    public static class UnityGameContentPaths
    {
        public static GameContentPaths Current { get; } = new GameContentPaths(Application.isEditor
            ? Path.Combine(Application.dataPath, "..", "..", "Content")
            : Application.streamingAssetsPath);
    }
}
