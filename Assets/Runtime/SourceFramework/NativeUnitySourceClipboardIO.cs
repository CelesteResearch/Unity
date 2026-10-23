using Celeste.Input.Devices.Text;
using UnityEngine;

namespace Celeste.InputRuntime
{
    public sealed class NativeUnitySourceClipboardIO : ISourceClipboardIO
    {
        public string GetClipboardText() => GUIUtility.systemCopyBuffer;
        public void SetClipboardText(string value) => GUIUtility.systemCopyBuffer = value;
    }
}
