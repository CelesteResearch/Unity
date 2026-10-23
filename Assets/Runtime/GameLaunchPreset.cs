using Celeste.Simulation;
using UnityEngine;

namespace Celeste.Unity
{
    [CreateAssetMenu(menuName = "Celeste/Launch Preset", fileName = "LaunchPreset")]
    public sealed class GameLaunchPreset : ScriptableObject
    {
        public global::Celeste.Game.Launch.GameLaunchOptions Options =
            new global::Celeste.Game.Launch.GameLaunchOptions();
    }
}
