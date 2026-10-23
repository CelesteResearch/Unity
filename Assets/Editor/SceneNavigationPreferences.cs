using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Celeste.Graphics.Scene.Diagnostics;
using UnityEditor;

namespace Celeste.Unity.Editor
{
    internal sealed class SceneNavigationPreferences
    {
        private readonly string keyPrefix;

        internal SceneNavigationPreferences(string assetsPath)
        {
            var path = Path.GetFullPath(assetsPath).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar).Replace('\\', '/');
            if (Path.DirectorySeparatorChar == '\\') path = path.ToUpperInvariant();
            using var hash = SHA256.Create();
            keyPrefix = "Celeste.SceneNavigation." +
                        BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(path))).Replace("-", "") + ".";
        }

        internal CelesteSceneNavigation.FollowMode ReadFollowMode()
        {
            var value = EditorPrefs.GetInt(keyPrefix + "Follow", (int)CelesteSceneNavigation.FollowMode.Room);
            return value switch
            {
                (int)CelesteSceneNavigation.FollowMode.Off => CelesteSceneNavigation.FollowMode.Off,
                (int)CelesteSceneNavigation.FollowMode.Player => CelesteSceneNavigation.FollowMode.Player,
                (int)CelesteSceneNavigation.FollowMode.Room => CelesteSceneNavigation.FollowMode.Room,
                (int)CelesteSceneNavigation.FollowMode.Map => CelesteSceneNavigation.FollowMode.Map,
                (int)CelesteSceneNavigation.FollowMode.GameView => CelesteSceneNavigation.FollowMode.GameView,
                _ => CelesteSceneNavigation.FollowMode.Room
            };
        }

        internal SceneTileScope ReadScope()
        {
            var value = EditorPrefs.GetInt(keyPrefix + "Scope", (int)SceneTileScope.VisibleRooms);
            return value switch
            {
                (int)SceneTileScope.VisibleWorld => SceneTileScope.VisibleWorld,
                (int)SceneTileScope.VisibleRooms => SceneTileScope.VisibleRooms,
                (int)SceneTileScope.EntireMap => SceneTileScope.EntireMap,
                _ => SceneTileScope.VisibleRooms
            };
        }

        internal bool ReadShowMap() => EditorPrefs.GetBool(keyPrefix + "ShowMap", true);
        internal bool ReadShowMarkers() => EditorPrefs.GetBool(keyPrefix + "ShowMarkers", true);
        internal bool ReadShowNames() => EditorPrefs.GetBool(keyPrefix + "ShowNames", true);

        internal void WriteFollowMode(CelesteSceneNavigation.FollowMode mode) =>
            EditorPrefs.SetInt(keyPrefix + "Follow", (int)mode);

        internal void WriteScope(SceneTileScope scope) => EditorPrefs.SetInt(keyPrefix + "Scope", (int)scope);
        internal void WriteShowMap(bool value) => EditorPrefs.SetBool(keyPrefix + "ShowMap", value);
        internal void WriteShowMarkers(bool value) => EditorPrefs.SetBool(keyPrefix + "ShowMarkers", value);
        internal void WriteShowNames(bool value) => EditorPrefs.SetBool(keyPrefix + "ShowNames", value);
    }
}
