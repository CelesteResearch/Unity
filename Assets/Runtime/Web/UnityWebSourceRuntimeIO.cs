using System;
using System.IO;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using Celeste.Core.Framework.Startup;
using Celeste.Core.IO.Platform.SDL;
using Celeste.Core.IO.Console;
using Celeste.Core.Diagnostics.Runtime;
using Celeste.Core.Randomness;
using Celeste.Everest.Modules.Persistence.GameSaves;
using Celeste.Everest.Runtime.Failures;
using Celeste.Everest.Runtime.Processes;
using Celeste.Framework.Startup;
using Celeste.Game.Execution.Diagnostics;
using Celeste.Input.Devices.Text;
using UnityEngine;

namespace Celeste.Unity.Web
{
    internal sealed class UnityWebSourceRuntimeIO : IEverestUserIOPathDevice, IOriginalErrorLogIo,
        ISourceFatalExitIo, IEverestProcessStartIo, ISourceSDLRuntimeDevice, ISourceRuntimeErrorDevice,
        ISourceGameHostRuntimeIO, ISourceWindowFocusDevice, ISourceConsoleModeIO, ISourceClipboardIO,
        ISourceRuntimeMemoryCapacity, ISourceRandomSeedDevice
    {
        private readonly Action<int> exit;

        public UnityWebSourceRuntimeIO(string persistentDirectory, Action<int> exit)
        {
            if (!Path.IsPathFullyQualified(persistentDirectory) || persistentDirectory.Contains("://"))
                throw new ArgumentException("A mounted persistent filesystem directory is required.",
                    nameof(persistentDirectory));
            ApplicationBaseDirectory = Path.GetFullPath(persistentDirectory);
            Directory.CreateDirectory(ApplicationBaseDirectory);
            this.exit = exit ?? throw new ArgumentNullException(nameof(exit));
        }

        public string ApplicationBaseDirectory { get; }

        public string ExecutingAssemblyLocation =>
            throw new PlatformNotSupportedException("Web has no physical owner DLL.");

        public bool IsLinux => false;
        public bool IsOSX => false;
        public string GetEnvironmentVariable(string name) => null;
        public string GetPlatform() => "Emscripten";
        public string GetPrefPath(string organization, string application) => ApplicationBaseDirectory;
        public bool FileExists(string path) => File.Exists(Resolve(path));
        public bool DirectoryExists(string path) => Directory.Exists(Resolve(path));
        public void CreateDirectory(string path) => Directory.CreateDirectory(Resolve(path));
        public TextReader OpenReader(string path) => new StreamReader(Resolve(path));

        public TextWriter OpenWriter(string path, bool append)
        {
            string resolved = Resolve(path);
            Directory.CreateDirectory(Path.GetDirectoryName(resolved));
            return new StreamWriter(resolved, append);
        }

        public void OpenDocument(string path) => Debug.Log(File.ReadAllText(Resolve(path)));
        public void Exit(int exitCode) => exit(exitCode);

        public void Start(ProcessStartInfo info)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            if (!info.UseShellExecute || !string.IsNullOrEmpty(info.Arguments) ||
                !Uri.TryCreate(info.FileName, UriKind.Absolute, out var uri)
                || (uri.Scheme != "https" && uri.Scheme != "http"))
                throw new PlatformNotSupportedException("Only browser HTTP(S) document navigation is available.");
            Application.OpenURL(uri.AbsoluteUri);
        }

        public void ShowRuntimeError(string title, string message) => Debug.LogError(title + "\n" + message);
        public bool GetTouchDeviceConnected() => UnityEngine.InputSystem.Touchscreen.current != null;
        public void SetMouseVisible(bool visible) => Cursor.visible = visible;

        public void SetHint(string name, string value) =>
            throw new PlatformNotSupportedException("SDL window hints are unavailable on browser canvas.");

        public void RaiseWindow(IntPtr window) =>
            throw new PlatformNotSupportedException("Browser focus requires a user gesture.");

        // An absent OS console is represented by the original API's failure status.
        public IntPtr GetStdHandle(int kind) => IntPtr.Zero;

        public bool GetConsoleMode(IntPtr handle, out uint mode)
        {
            mode = 0;
            return false;
        }

        public bool SetConsoleMode(IntPtr handle, uint mode) => false;

        public string GetClipboardText() =>
            throw new PlatformNotSupportedException(
                "Browser clipboard reads require an asynchronous permission boundary.");

        public void SetClipboardText(string value) =>
            throw new PlatformNotSupportedException(
                "Browser clipboard writes require an asynchronous permission boundary.");

        public long ReadTotalAvailableMemoryBytes() =>
            throw new PlatformNotSupportedException("Browser process memory capacity is unavailable.");

        public int TickCount => Environment.TickCount;
        public void FillEntropy(Span<byte> destination) => UnityWebBrowserInterop.FillEntropy(destination);

        private string Resolve(string path)
        {
            if (path == null || path.Contains("://"))
                throw new ArgumentException("A persistent file path is required.", nameof(path));
            string resolved = Path.GetFullPath(Path.Combine(ApplicationBaseDirectory, path));
            string prefix = ApplicationBaseDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (resolved != ApplicationBaseDirectory && !resolved.StartsWith(prefix, StringComparison.Ordinal))
                throw new ArgumentException("Error-log IO must stay within the mounted persistent filesystem.",
                    nameof(path));
            return resolved;
        }
    }
}
