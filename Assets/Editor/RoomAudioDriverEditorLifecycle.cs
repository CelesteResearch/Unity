using System;
using System.Collections.Generic;
using Celeste.Unity;
using UnityEditor;
using UnityEngine;
using Celeste.Audio.Fmod.Gameplay.Audio;

namespace Celeste.Unity.Editor
{
    [InitializeOnLoad]
    internal static class RoomAudioDriverEditorLifecycle
    {
        private static readonly object Gate = new object();
        private static readonly HashSet<RoomAudioDriver> Drivers = new HashSet<RoomAudioDriver>();

        static RoomAudioDriverEditorLifecycle()
        {
            RoomAudioDriver.Created += Register;
            RoomAudioDriver.Disposing += Unregister;
            AssemblyReloadEvents.beforeAssemblyReload += DisposeBeforeReload;
        }

        private static void Register(RoomAudioDriver driver)
        {
            lock (Gate) Drivers.Add(driver);
        }

        private static void Unregister(RoomAudioDriver driver)
        {
            lock (Gate) Drivers.Remove(driver);
        }

        private static void DisposeBeforeReload()
        {
            RoomAudioDriver[] snapshot;
            lock (Gate)
            {
                snapshot = new RoomAudioDriver[Drivers.Count];
                Drivers.CopyTo(snapshot);
            }

            foreach (RoomAudioDriver driver in snapshot)
            {
                try
                {
                    driver.Dispose();
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                }
            }
        }
    }
}
