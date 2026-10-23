using Celeste.Graphics.Direct3D11.Devices;
using Celeste.Graphics.Direct3D12.Devices;
using Celeste.Graphics.Rendering.Devices;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading;
using Celeste.Game.Runtime;
using Celeste.Game.Profiles;
using Celeste.Game.Execution.Startup;
using Celeste.Unity.Input;
#if !UNITY_WEBGL || UNITY_EDITOR
using Celeste.InputRuntime;
using Celeste.Graphics.Vulkan.Devices;
using Celeste.Core.Framework.Loading;
using Celeste.Core.Framework.Startup;
using Celeste.Graphics.Effects;
using System.Threading.Tasks;
using Celeste.Unity.Graphics.SourceDevice;
#endif
using Celeste.Platform.Native.Audio.FAudio;
using Celeste.Platform.Native.Audio.Capture;
using Celeste.Platform.Native.Media.Theora;
using Celeste.Platform.Native.Images.Fna;
using Celeste.Platform.Native.Sdl;
using Celeste.Platform.Native.Timing;
using Celeste.Platform.Native.Randomness;
using Celeste.Platform.Native.Console;
using Celeste.Platform.Native.Displays;
using UnityEngine;
using UnityEngine.Rendering;
using global::Celeste.Game.Execution;

namespace Celeste.Unity
{
    [DisallowMultipleComponent]
    public sealed class GameHost : MonoBehaviour
    {
#if UNITY_EDITOR
        public UnityNativeTextureBatch CreateSceneTextureBatch(Celeste.Graphics.Rendering.Devices.Native.NativeGraphicsDeviceIO device)
            => device is Direct3D12GraphicsDeviceIO ? direct3D12Submission?.CreateTextureBatch(device) :
                device is VulkanGraphicsDeviceIO vulkan ? metalSubmission != null ? metalSubmission.CreateTextureBatch(vulkan) : vulkanSubmission?.CreateTextureBatch(vulkan) : null;
#endif
        [SerializeField] private Camera activeGameCamera;
        [SerializeField] private GameProfile sourceProfile = GameProfile.Original;
#if !UNITY_WEBGL || UNITY_EDITOR
        private GameApplication actualProcess;
        private NativeUnitySourceSdkInputDevices inputDevices;
        private NativeUnitySourceSdkInputBridge inputBridge;
        private UnityDirect3D11CameraOutput cameraOutput;
        private UnityDirect3D12GraphicsSubmission direct3D12Submission;
        private Direct3D12GraphicsDeviceIOFactory direct3D12Factory;
        private UnityVulkanGraphicsSubmission vulkanSubmission;
        private VulkanGraphicsDeviceIOFactory vulkanFactory;
        private UnityMetalGraphicsSubmission metalSubmission;
#if UNITY_EDITOR
        private UnityEditorSourceWindowIO editorWindow;
#else
        private NativeUnitySourceWindowIO playerWindow;
        private bool resolutionChanging;
        private bool resolutionReady;
        private bool previousCameraEnabled;
#endif
        private ISourceGraphicsAdapterDisplayIO displayIO;
        private bool disposed;
#endif
#if UNITY_WEBGL
        [SerializeField] private string webManifestUrl = "./Content/manifest.json";
        [SerializeField] private string webContentBaseUrl = "./Content/Celeste/";
        [SerializeField] private string webFrameworkResourcesUrl =
 "./Content/FrameworkTimeResources/mscorlib.resources";
#endif
#if UNITY_WEBGL && !UNITY_EDITOR
        private Web.UnityWebGameHost webHost;
#endif
#if !UNITY_WEBGL || UNITY_EDITOR
        private Memory.NativeUnitySourceGarbageCollectionDevice garbageCollection;

        private global::Celeste.Game.Runtime.Physical.Networking.SourceMonoCompressedHttpTransport
            compressedHttpTransport;
#endif

        public RenderTexture GameOutput =>
#if UNITY_WEBGL && !UNITY_EDITOR
            webHost?.GameOutput;
#else
            disposed ? null :
            direct3D12Submission != null ? direct3D12Submission.GameOutput :
            vulkanSubmission != null ? vulkanSubmission.GameOutput :
            metalSubmission != null ? metalSubmission.GameOutput :
            cameraOutput?.ExistingOutput;
#endif

        public GameApplication ActualProcess =>
#if UNITY_WEBGL && !UNITY_EDITOR
            webHost?.ActualProcess ??
#else
            actualProcess ??
#endif
            throw new InvalidOperationException(
                "The shared source application has not been constructed.");

        private void Awake()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (sourceProfile != GameProfile.Original) throw new NotSupportedException("The Web host currently supports the complete Original profile only.");
            webHost = gameObject.AddComponent<Web.UnityWebGameHost>();
            webHost.Configure(activeGameCamera, webManifestUrl, webContentBaseUrl, webFrameworkResourcesUrl);
            return;
#else
            if (activeGameCamera == null)
                throw new InvalidOperationException("The scene must bind its one active game camera.");
            bool useDirect3D12Graphics =
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12;
            bool useVulkanGraphics = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan;
            bool useMetalGraphics = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal;
            if (!useDirect3D12Graphics && !useVulkanGraphics && !useMetalGraphics &&
                SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11)
                throw new NotSupportedException(
                    "This source physical transport requires Direct3D11, Vulkan, Direct3D12 or Metal.");
            garbageCollection = new Memory.NativeUnitySourceGarbageCollectionDevice();
            var profile = GameProfileSelection.Resolve(sourceProfile, Environment.GetCommandLineArgs());
            var mainContext = SynchronizationContext.Current ??
                              throw new InvalidOperationException(
                                  "The actual Unity main synchronization context is required.");
            int mainThread = Thread.CurrentThread.ManagedThreadId;
#if UNITY_EDITOR
            editorWindow = new UnityEditorSourceWindowIO(activeGameCamera);
            var window = editorWindow;
#else
            playerWindow = new NativeUnitySourceWindowIO(NativeUnitySourceWindowIO.BorrowCurrentUnityPlayerWindow());
            var window = playerWindow;
#endif
            inputDevices = new NativeUnitySourceSdkInputDevices(window);
            ISourceGraphicsDeviceIOFactory graphicsFactory;
            ISourceHostDrawTransportIO drawTransport;
            ISourceLoadingWorkScheduler loadingWorkScheduler = null;
            if (useMetalGraphics)
            {
                metalSubmission = new UnityMetalGraphicsSubmission(activeGameCamera);
                vulkanFactory = new VulkanGraphicsDeviceIOFactory(metalSubmission, window.Handle);
                graphicsFactory = vulkanFactory;
                drawTransport = vulkanFactory;
                loadingWorkScheduler =
                    CooperativeSourceLoadingWorkScheduler.CreateWithContextPreparation(_ => default(ValueTask));
            }
            else if (useVulkanGraphics)
            {
                vulkanSubmission = new UnityVulkanGraphicsSubmission(activeGameCamera);
                vulkanFactory = new VulkanGraphicsDeviceIOFactory(vulkanSubmission, window.Handle);
                graphicsFactory = vulkanFactory;
                drawTransport = vulkanFactory;
                loadingWorkScheduler =
                    CooperativeSourceLoadingWorkScheduler.CreateWithContextPreparation(_ => default(ValueTask));
            }
            else if (useDirect3D12Graphics)
            {
                direct3D12Submission = new UnityDirect3D12GraphicsSubmission(activeGameCamera);
                direct3D12Factory = new Direct3D12GraphicsDeviceIOFactory(direct3D12Submission, window.Handle);
                graphicsFactory = direct3D12Factory;
                drawTransport = direct3D12Factory;
                // Local desktop content is already readable; only Unity resource work changes owner thread.
                loadingWorkScheduler =
                    CooperativeSourceLoadingWorkScheduler.CreateWithContextPreparation(_ => default(ValueTask));
            }
            else
            {
                cameraOutput = new UnityDirect3D11CameraOutput(activeGameCamera, mainThread);
                var submission = new UnityDirect3D11GraphicsSubmission(mainContext, mainThread, cameraOutput);
                var nativeGraphicsFactory =
                    new Direct3D11GraphicsDeviceIOFactory(submission, window.Handle, new NativeSourceSDLHintDevice());
                graphicsFactory = nativeGraphicsFactory;
                drawTransport = nativeGraphicsFactory;
            }

            string physicalOwnerAssemblyPath = typeof(GameRuntime).Assembly.GetLoadedAssemblyPath();
            if (!File.Exists(physicalOwnerAssemblyPath))
                throw new FileNotFoundException("The physical managed owner DLL must be deployed.",
                    physicalOwnerAssemblyPath);
            var contentPaths = Content.UnityGameContentPaths.Current;
            string contentRoot = contentPaths.Game;
            var runtime = new NativeUnitySourceRuntimeIO(window, physicalOwnerAssemblyPath);

            var clock = new SourceStopwatchGameClockDevice();
#if UNITY_EDITOR
            displayIO = new UnityEditorSourceDisplayIO(activeGameCamera);
#else
            displayIO = new NativeSourceDisplayIO();
#endif
            if (profile == GameProfile.InstalledEverest)
            {
                compressedHttpTransport =
                    new global::Celeste.Game.Runtime.Physical.Networking.SourceMonoCompressedHttpTransport();
                runtime.BindCompressedHttpCreationFrame(global::Celeste.Game.Runtime.Physical.Networking
                    .SourceMonoCompressedHttpTransport.HookCreationFrame);
            }

            var physical = new global::Celeste.Game.Runtime.Platform.GamePlatformServices(
                physicalOwnerAssemblyPath, contentRoot, window, graphicsFactory, displayIO,
                new Celeste.Core.IO.Content.ProjectedContentSource(
                    new Celeste.Core.IO.Content.DirectoryContentSource(contentRoot),
                    new System.Collections.Generic.Dictionary<string, string> { { ".meta", ".atlas.bytes" } },
                    new[] { ".meta" }),
                new NativeSourceFnaImageCodecDevice(),
                inputDevices.Keyboard, inputDevices.MouseTouch, inputDevices.GamePads,
                clock, clock, new NativeSourceRandomSeeds(), new NativeSourceFAudioIOFactory(),
                new NativeSourceMicrophoneIO(), new NativeSourceTheoraIO(), runtime, inputDevices.InputClock,
                new NativeSourceFrameworkTimeResources(contentPaths.FrameworkTimeResources),
                runtime, runtime, new NativeSourceSDLRuntimeDevice(), runtime, runtime,
                null, runtime, runtime,
                profile == GameProfile.InstalledEverest
                    ? new global::Celeste.Game.Runtime.Physical.Processes.ProcessSourceEverestSplashIO()
                    : null, new NativeSourceConsoleModeIO(),
                drawTransport, runtime, inputDevices.TextInputPlatform,
                new global::Celeste.Game.Runtime.Physical.Runtime.ManagedSourceRuntimeDiagnostics(
                    new global::Celeste.Game.Runtime.Physical.Memory.NativeSourceRuntimeMemoryCapacity()), null,
                new NativeUnitySourceClipboardIO(), compressedHttpTransport, garbageCollection: garbageCollection,
                temporaryDirectoryPath: Application.temporaryCachePath,
                loadingWorkScheduler: loadingWorkScheduler);
            actualProcess = GameApplicationFactory.Create(profile, physical);
            if (actualProcess == null)
            {
                enabled = false;
                QuitHost();
                return;
            }

            if (profile == GameProfile.InstalledEverest)
            {
                actualProcess.BindSourceInitializationSaveLinkIO(new NativeSourceDirectoryLinkTargetIO());
            }

            inputBridge = new NativeUnitySourceSdkInputBridge(inputDevices, ActualProcess.ActualGame,
                ActualProcess.KeyboardState, ActualProcess.FnaInput, ActualProcess.FnaGamePads,
                ActualProcess.TextBindings, ActualProcess.TextInput);
            clock.BindPollEvents(inputBridge.PollEvents);
#endif
        }

        private IEnumerator Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            yield break;
#else
            yield return null;
            try
            {
                if (actualProcess != null && !disposed) ActualProcess.ActualGame.StartSourceHostRun();
            }
            catch
            {
                AbortUnsubmittedDraws();
                throw;
            }
#endif
        }

        private void Update()
        {
#if !UNITY_WEBGL || UNITY_EDITOR

            try
            {
                if (actualProcess != null && !disposed && actualProcess.ActualGame.SourceHostRunOpen)
                {
#if !UNITY_EDITOR
                    if (resolutionChanging)
                    {
                        if (!resolutionReady) return;
                        if (playerWindow.HasPendingResolutionChange)
                        {
                            resolutionReady = false;
                            StartCoroutine(ChangePlayerResolution());
                            return;
                        }

                        activeGameCamera.enabled = previousCameraEnabled;
                        if (vulkanSubmission != null) vulkanSubmission.PrepareDraw();
                        else if (direct3D12Submission != null) direct3D12Submission.PrepareDraw();
                        else if (metalSubmission != null) metalSubmission.PrepareDraw();
                        else cameraOutput.EnsureOutput();
                        resolutionReady = false;
                        resolutionChanging = false;
                    }
#endif
#if UNITY_EDITOR
                    if (editorWindow.ObserveDrawableSizeChange())
                        ((Celeste.Graphics.Rendering.Devices.SourceHostGameWindow)actualProcess.ActualGame.Window)
                            .NotifyClientSizeChanged();
#endif
                    actualProcess.ActualGame.ContinueSourceHostRun();
                    garbageCollection.Advance(
                        ((Celeste.Core.Scheduling.ISourceEngineContext)actualProcess).Frame.Scenes.CurrentScene is
                        Celeste.Game.Execution.Levels.OriginalLevelSceneRoot);
                    if (!actualProcess.ActualGame.SourceHostRunOpen) QuitHost();
                }
            }
            catch
            {
#if !UNITY_EDITOR
                if (resolutionChanging)
                {
                    activeGameCamera.enabled = false;
                    enabled = false;
                    QuitHost();
                }
#endif
                AbortUnsubmittedDraws();
                throw;
            }
#endif
        }

        private void LateUpdate()
        {
#if !UNITY_WEBGL && !UNITY_EDITOR
            if (actualProcess == null || disposed || resolutionChanging ||
                !playerWindow.HasPendingResolutionChange) return;
            resolutionChanging = true;
            previousCameraEnabled = activeGameCamera.enabled;
            activeGameCamera.enabled = false;
            StartCoroutine(ChangePlayerResolution());
#endif
        }

#if !UNITY_WEBGL && !UNITY_EDITOR
        private IEnumerator ChangePlayerResolution()
        {
            yield return new WaitForEndOfFrame();
            yield return null;
            if (disposed) yield break;
            ApplyPlayerResolutionChange();
            double deadline = Time.realtimeSinceStartupAsDouble + 15;
            int stableFrame = -1;
            while (!disposed)
            {
                yield return null;
                if (disposed) yield break;
                if (Time.realtimeSinceStartupAsDouble >= deadline)
                {
                    enabled = false;
                    QuitHost();
                    throw new InvalidOperationException("The Unity window did not reach the requested display mode.");
                }

                if (!playerWindow.AppliedResolutionMatches)
                {
                    stableFrame = -1;
                    continue;
                }

                if (stableFrame < 0)
                {
                    stableFrame = Time.frameCount;
                    continue;
                }

                if (Time.frameCount == stableFrame) continue;
                if (playerWindow.HasPendingResolutionChange)
                {
                    ApplyPlayerResolutionChange();
                    deadline = Time.realtimeSinceStartupAsDouble + 15;
                    stableFrame = -1;
                    continue;
                }

                resolutionReady = true;
                yield break;
            }
        }

        private void ApplyPlayerResolutionChange()
        {
            try
            {
                vulkanSubmission?.DrainForWindowChange();
                playerWindow.ApplyPendingResolution();
            }
            catch
            {
                enabled = false;
                QuitHost();
                throw;
            }
        }
#endif

        private void AbortUnsubmittedDraws()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (direct3D12Factory != null) direct3D12Factory.AbortUnsubmittedDraws();
            else if (vulkanFactory != null) vulkanFactory.AbortUnsubmittedDraws();
            else Direct3D11GraphicsDeviceIO.AbortUnsubmittedPhysicalPrefix();
#endif
        }

        private static void QuitHost()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnApplicationFocus(bool active)
        {
#if !UNITY_WEBGL || UNITY_EDITOR

            if (actualProcess != null && !disposed) actualProcess.ActualGame.IsActive = active;
#endif
        }

        private void OnApplicationQuit()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (actualProcess != null && !disposed) actualProcess.ActualGame.GameLoop.Exit();
            DisposeHost();
#endif
        }

        private void OnDestroy() => DisposeHost();

        private void DisposeHost()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (disposed) return;
            disposed = true;
            try
            {
                if (actualProcess != null)
                {
                    try
                    {
                        try
                        {
                            actualProcess.ActualGame.FinishSourceHostRun();
                        }
                        finally
                        {
                            if (direct3D12Submission != null || vulkanSubmission != null || metalSubmission != null)
                            {
                                ((CooperativeSourceLoadingWorkScheduler)actualProcess.PhysicalIO.LoadingWorkScheduler)
                                    .DrainReadyWork();
                                actualProcess.WaitSourceBackgroundThreads(actualProcess.PhysicalIO.LoadingWorkScheduler
                                    .Pump);
                            }
                        }
                    }
                    finally
                    {
                        try
                        {
                            // Keep output available for source disposal after detaching the camera's flushed prefix.
                            if (direct3D12Submission != null) direct3D12Submission.StopCameraCopy();
                            else if (vulkanSubmission != null) vulkanSubmission.StopCameraCopy();
                            else if (metalSubmission != null) metalSubmission.StopCameraCopy();
                            else cameraOutput?.StopCameraCopy();
                        }
                        finally
                        {
                            try
                            {
                                inputBridge?.Dispose();
                            }
                            finally
                            {
                                actualProcess.ActualGame.Dispose();
                            }
                        }
                    }
                }
                else inputBridge?.Dispose();
            }
            finally
            {
                try
                {
                    compressedHttpTransport?.Dispose();
                }
                finally
                {
                    try
                    {
                        inputDevices?.Dispose();
                    }
                    finally
                    {
                        try
                        {
                            if (direct3D12Submission != null) direct3D12Submission.Dispose();
                            else if (vulkanSubmission != null) vulkanSubmission.Dispose();
                            else if (metalSubmission != null) metalSubmission.Dispose();
                            else cameraOutput?.Dispose();
                        }
                        finally
                        {
                            (displayIO as IDisposable)?.Dispose();
                        }
                    }
                }
            }
#endif
        }
    }
}
