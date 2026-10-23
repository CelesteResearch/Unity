using System;
using UnityEngine;

namespace Celeste.Unity.Graphics.SourceDevice
{
    // Outputs and packets stay on the backend's actual Unity graphics owner.
    internal interface IUnityNativeTextureExportHost
    {
        bool IsTextureGenerationCurrent(ulong expected);
        IntPtr CreateTextureOutput(Texture2D texture);
        IntPtr AcquireTextureOutputLoan(IntPtr cache);
        void ReleaseTextureOutputCache(IntPtr cache);
        void SubmitTextureBatch(IntPtr packet, IntPtr[] outputs, int count);
        void DrainTextureOutputs(ulong expected);
    }
}
