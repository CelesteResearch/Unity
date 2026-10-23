using Celeste.Game.Save.Storage;
using System;
using System.IO;
using UnityEngine;

namespace Celeste.Unity
{
    // Select the profile before the first loader builds its FileSaveStorage. A late
    // change to a presentation field cannot change an already-bound menu world.
    internal static class GameSaveLocation
    {
        internal static string DefaultDirectory
        {
            get
            {
                string configured = Environment.GetEnvironmentVariable("CELESTE_SAVE_DIRECTORY");
                return string.IsNullOrWhiteSpace(configured)
                    ? Path.Combine(Application.persistentDataPath, "Saves")
                    : Path.GetFullPath(configured);
            }
        }
    }
}
