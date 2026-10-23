using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Celeste.Core.IO.Content;
using Celeste.Core.IO.Content.Manifests;
using Celeste.Game.Runtime;
using Celeste.Game.Runtime.Platform;
using Celeste.Game.Runtime.Physical.Runtime;
using Celeste.Game.Runtime.Content.Loading;
using Celeste.Game.Menus.Loading;
using Celeste.Graphics.Rendering.Devices;
using Celeste.Unity.Content;
using Celeste.Unity.Input;
using Celeste.Unity.Runtime.Timing;
using Celeste.Unity.Web.Content;
using UnityEngine;
using UnityEngine.Networking;

namespace Celeste.Unity.Web
{
    [DisallowMultipleComponent]
    public sealed class UnityWebGameHost : MonoBehaviour
    {
        // Root registers the concrete managed graphics presenter and official FMOD Web backend.
        public static Func<Camera, UnityWebSourceWindowIO, Func<ContentFileInfo, Uri>, CancellationToken,
                Task<UnityWebGraphicsAudioDevices>>
            CreateGraphicsAudioDevicesAsync { get; set; }

        private Camera camera;
        private Uri manifestUri, contentUri, frameworkTimeUri;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private UnityContentTransport transport;
        private ResidentContentSource source;
        private UnityWebGraphicsAudioDevices graphicsAudio;
        private UnityWebSourceSdkInputDevices input;
        private NativeUnitySourceSdkInputBridge inputBridge;
        private UnityWebSourceWindowIO window;
        private UnitySourceGameClockDevice clock;
        private GameApplication game;
        private ProgressiveOriginalContentBootstrap bootstrap;
        private OriginalContentLoadingPresenter contentLoading;
        private UnityWebStoreMenuOwner storeMenu;
        private UnityWebContentDownloadController contentDownloads;
        private bool configured, stopped, started, closing;
        private bool readyReported;
        private GameLoaderScene startupLoader;
        private Task stopTask;
        private Task exitTask;
        private Task persistenceTask;
        private long persistedFileMutationVersion;
        private double nextProgressAt;
        private Memory.NativeUnitySourceGarbageCollectionDevice garbageCollection;

        public GameApplication ActualProcess =>
            game ?? throw new InvalidOperationException("The Web source game is not constructed.");

        public RenderTexture GameOutput => stopped ? null : graphicsAudio?.ReadOutput();

        public Task Startup { get; private set; }

        public void Configure(Camera activeCamera, string manifestUrl, string contentBaseUrl,
            string frameworkResourcesUrl)
        {
            if (configured) throw new InvalidOperationException("The Web host is already configured.");
            camera = activeCamera ?? throw new ArgumentNullException(nameof(activeCamera));
            manifestUri = RequireHttp(manifestUrl);
            contentUri = RequireHttp(contentBaseUrl);
            frameworkTimeUri = RequireHttp(frameworkResourcesUrl);
            configured = true;
        }

        private void Start()
        {
            if (!configured) throw new InvalidOperationException("Configure the Web host before its Start callback.");
            Startup = StartAsync();
            _ = ReportFailureAsync(Startup);
        }

        private async Task StartAsync()
        {
            var createGraphicsAudio = CreateGraphicsAudioDevicesAsync ??
                                      throw new InvalidOperationException(
                                          "Configure the real graphics and FMOD module readiness factory before startup.");
            // Populate actual IDBFS before Factory synchronously reads settings/saves.
            Debug.Log("Celeste Web startup: restoring persistent files.");
            UnityWebBrowserInterop.ReportStartupStage("restore-saves");
            await UnityWebBrowserInterop.SyncPersistentFilesAsync(true);
            lifetime.Token.ThrowIfCancellationRequested();
            Debug.Log("Celeste Web startup: reading the content manifest.");
            UnityWebBrowserInterop.ReportStartupStage("read-manifest");
            transport = new UnityContentTransport(contentUri);
            var manifest = await transport.ReadManifestAsync(manifestUri, lifetime.Token);
            lifetime.Token.ThrowIfCancellationRequested();
            Debug.Log("Celeste Web startup: loading framework resources.");
            UnityWebBrowserInterop.ReportStartupStage("prepare-framework");
            source = ResidentContentSource.FromVerifiedLoader("/CelesteContent", manifest,
                transport.LoadVerifiedOwnedBytesAsync);
            byte[] timeResources = await ReadFrameworkResourcesAsync(frameworkTimeUri, lifetime.Token);
            lifetime.Token.ThrowIfCancellationRequested();
            window = new UnityWebSourceWindowIO(camera);
            input = new UnityWebSourceSdkInputDevices(window);
            clock = new UnitySourceGameClockDevice();
            Debug.Log("Celeste Web startup: initializing graphics and audio.");
            UnityWebBrowserInterop.ReportStartupStage("initialize-devices");
            var creation = createGraphicsAudio(camera, window, transport.GetContentUri, lifetime.Token) ??
                           throw new InvalidOperationException("Graphics/audio readiness returned no task.");
            // The factory owns unpublished resources. A returned holder transfers ownership here,
            // even when cancellation races with the real module's ready callback.
            graphicsAudio = await creation ??
                            throw new InvalidOperationException("Physical graphics/audio creation returned no owner.");
            lifetime.Token.ThrowIfCancellationRequested();
            Debug.Log("Celeste Web startup: preparing boot resources.");
            UnityWebBrowserInterop.ReportStartupStage("prepare-boot");
            bootstrap = new ProgressiveOriginalContentBootstrap(source,
                (info, token) => graphicsAudio.PrepareAudioBanks(info.Name, source, token), lifetime.Token);
            clock.BindSourceUpdateBlock(() => bootstrap.Scheduler.SourceUpdatesBlocked);
            await bootstrap.PrepareBootAsync();
            lifetime.Token.ThrowIfCancellationRequested();
            var deployment = new CompiledSourceDeployment("/CelesteRuntime");
            var commandLine =
                new CompiledSourceRuntimeIO(deployment, new[] { "Celeste" }, StartPreparedSourceHostRun, _ => null);
            var runtime = new UnityWebSourceRuntimeIO(Application.persistentDataPath, RequestExit);
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                storeMenu = new UnityWebStoreMenuOwner(() => game, runtime);
                contentDownloads = new UnityWebContentDownloadController(manifestUri, contentUri);
            }

            var frameworkAudio = new UnityWebUnavailableFrameworkAudio();
            garbageCollection = new Memory.NativeUnitySourceGarbageCollectionDevice();
            var physical = new GamePlatformServices(null, source.TitleLocation, window, graphicsAudio.GraphicsFactory,
                window,
                source, new UnityWebSourceImageCodec(), input.Keyboard, input.MouseTouch, input.GamePads,
                clock, clock, runtime, frameworkAudio, frameworkAudio, frameworkAudio, runtime, input,
                new UnityWebFrameworkTimeResources(timeResources), runtime, runtime, runtime, commandLine, runtime,
                null, runtime, runtime, null, runtime, graphicsAudio.DrawTransport, runtime, input,
                new SourceRuntimeDiagnostics(runtime), null, runtime, null,
                garbageCollection: garbageCollection, temporaryDirectoryPath: Application.temporaryCachePath,
                compiledSourceDeployment: deployment, loadingWorkScheduler: bootstrap.Scheduler,
                studioAudioDevice: graphicsAudio.StudioAudio, audioBankContent: null,
                mainMenuAdditionalButton: storeMenu?.Button,
                menuOptionsExtension: contentDownloads == null
                    ? null
                    : new UnityWebSaveTransferMenu(this, new UnityWebContentDownloadMenu(contentDownloads)));
            Debug.Log("Celeste Web startup: constructing the original game.");
            UnityWebBrowserInterop.ReportStartupStage("construct-game");
            game = bootstrap.CreatePreparedGame(physical);
            inputBridge = new NativeUnitySourceSdkInputBridge(input, game.ActualGame, game.KeyboardState,
                game.FnaInput, game.FnaGamePads, game.TextBindings, game.TextInput, PhysicalInputEnabled);
            input.BindTouchController(instanceId =>
            {
                if (PhysicalInputEnabled() && game.FnaGamePads.Platform.TryGetPlayerIndex(instanceId, out int index))
                    game.InputSource.Gamepad = index;
            });
            clock.BindPollEvents(inputBridge.PollEvents);
            // Give Unity one frame to apply the source constructor's resolution request.
            await Awaitable.NextFrameAsync(lifetime.Token);
            lifetime.Token.ThrowIfCancellationRequested();
            StartPreparedSourceHostRun();
            Debug.Log("Celeste Web startup: waiting for the first source tick.");
            UnityWebBrowserInterop.ReportStartupStage("load-game");
        }

        private void StartPreparedSourceHostRun()
        {
            if (started || stopped || closing)
                throw new InvalidOperationException("The Web source run cannot start twice or after shutdown.");
            started = true;
            ActualProcess.ActualGame.StartSourceHostRun();
            startupLoader = game.Frame.Scenes.NextScene as GameLoaderScene ??
                            game.Frame.Scenes.CurrentScene as GameLoaderScene;
            contentLoading = new OriginalContentLoadingPresenter(game);
        }

        private void Update()
        {
            if (stopped || closing) return;
            try
            {
                if (source != null && Time.realtimeSinceStartupAsDouble >= nextProgressAt)
                {
                    nextProgressAt = Time.realtimeSinceStartupAsDouble + 0.25;
                    var contentProgress = source.ReadPreparationProgress(transport.ReceivedContentBytes,
                        transport.CachedContentBytes);
                    if (!readyReported) UnityWebBrowserInterop.ReportContentProgress(contentProgress);
                    contentLoading?.SetProgress(contentProgress, graphicsAudio.ReadAudioProgress());
                }

                if (game == null || !started) return;
                if (bootstrap.Scheduler.SourceUpdatesBlocked) clock.Pause();
                bootstrap.Scheduler.Pump();
                if (bootstrap.Scheduler.SourceUpdatesBlocked)
                {
                    clock.Pause();
                    contentLoading.PresentBlocked();
                    ReportStartupReady();
                    return;
                }

                clock.Resume();
                if (game.ActualGame.SourceHostRunOpen)
                {
                    if (window.ObserveDrawableSizeChange())
                        ((SourceHostGameWindow)game.ActualGame.Window).NotifyClientSizeChanged();
                    game.ActualGame.ContinueSourceHostRun();
                    ReportStartupReady();

                    if (bootstrap.Scheduler.SourceUpdatesBlocked) clock.Pause();
                    garbageCollection.Advance();
                    if (game.UserIOStorage.FileMutationVersion != persistedFileMutationVersion &&
                        (persistenceTask == null || persistenceTask.IsCompleted))
                    {
                        persistenceTask = PersistAsync();
                        _ = ReportFailureAsync(persistenceTask);
                    }
                }

                if (!game.ActualGame.SourceHostRunOpen) _ = ReportFailureAsync(ExitPageAsync());
            }
            catch
            {
                closing = true;
                try
                {
                    graphicsAudio?.AbortUnsubmittedDraws();
                }
                finally
                {
                    try
                    {
                        UnityWebBrowserInterop.NotifyGameFailed();
                    }
                    catch (Exception notificationException)
                    {
                        Debug.LogException(notificationException);
                    }
                    finally
                    {
                        _ = ReportFailureAsync(StopAsync());
                    }
                }

                throw;
            }
        }

        private void ReportStartupReady()
        {
            if (readyReported || !graphicsAudio.FirstSourcePresentationSubmitted) return;
            if (game.Frame.Scenes.CurrentScene == null || bootstrap.Scheduler.SourceUpdatesBlocked ||
                startupLoader != null && !startupLoader.SourceLoaded) return;
            readyReported = true;
            startupLoader = null;
            Debug.Log("Celeste Web startup: startup resources ready and source frame submitted.");
            UnityWebBrowserInterop.ReportStartupStage("ready");
            UnityWebBrowserInterop.NotifyGameReady();
        }

        public Task PersistSavesAsync()
        {
            if (persistenceTask == null || persistenceTask.IsCompleted) persistenceTask = PersistAsync();
            return persistenceTask;
        }

        private async Task PersistAsync()
        {
            if (stopped || game == null) throw new InvalidOperationException("There is no active Web save owner.");
            // Stop may clear the host field while waiting; this sync belongs to the captured save owner.
            var owner = game;
            await owner.WaitSourceBackgroundThreadsAsync();
            long version = owner.UserIOStorage.FileMutationVersion;
            await UnityWebBrowserInterop.SyncPersistentFilesAsync(false);
            // Writes during the asynchronous sync stay dirty and trigger the next sync after this task completes.
            persistedFileMutationVersion = version;
        }

        public Task StopAsync() => stopTask ??= StopCoreAsync();

        private Task ExitPageAsync()
        {
            return exitTask ??= ExitPageCoreAsync();
        }

        private async Task ExitPageCoreAsync()
        {
            await StopAsync();
            UnityWebBrowserInterop.CloseGamePage();
        }

        private async Task StopCoreAsync()
        {
            closing = true;
            // Await construction without allowing another run or resource creation after stop.
            if (Startup != null && !Startup.IsCompleted) lifetime.Cancel();
            try
            {
                if (Startup != null)
                {
                    try
                    {
                        await Startup;
                    }
                    catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
                    {
                    }
                }

                if (game != null)
                {
                    game.ActualGame.GameLoop.Exit();
                    await DrainSourceWorkAsync();
                    game.ActualGame.FinishSourceHostRun();
                    await DrainSourceWorkAsync();
                    DetachInputBridge();
                    game.ActualGame.Dispose();
                    game = null;
                }

                if (persistenceTask != null) await persistenceTask;
                await UnityWebBrowserInterop.SyncPersistentFilesAsync(false);
            }
            catch
            {
                graphicsAudio?.AbortUnsubmittedDraws();
                throw;
            }
            finally
            {
                try
                {
                    if (game != null)
                    {
                        try
                        {
                            game.ActualGame.FinishSourceHostRun();
                        }
                        finally
                        {
                            try
                            {
                                DetachInputBridge();
                            }
                            finally
                            {
                                game.ActualGame.Dispose();
                                game = null;
                            }
                        }
                    }
                }
                finally
                {
                    DisposeOwners();
                }
            }
        }

        private bool PhysicalInputEnabled() => !closing && !stopped && !bootstrap.Scheduler.SourceUpdatesBlocked;

        private void DetachInputBridge()
        {
            startupLoader = null;
            var loading = contentLoading;
            contentLoading = null;
            var menu = storeMenu;
            storeMenu = null;
            var downloads = contentDownloads;
            contentDownloads = null;
            var bridge = inputBridge;
            inputBridge = null;
            try
            {
                loading?.Dispose();
            }
            finally
            {
                try
                {
                    menu?.Dispose();
                }
                finally
                {
                    try
                    {
                        downloads?.Dispose();
                    }
                    finally
                    {
                        bridge?.Dispose();
                    }
                }
            }
        }

        private async Task DrainSourceWorkAsync()
        {
            Task drain = game.WaitSourceBackgroundThreadsAsync();
            while (!drain.IsCompleted)
            {
                bootstrap.Scheduler.Pump();
                await Awaitable.NextFrameAsync();
            }

            await drain;
        }

        private void RequestExit(int exitCode)
        {
            Debug.Log("Celeste Web requested exit " + exitCode);
            _ = ReportFailureAsync(exitCode == 0 ? ExitPageAsync() : StopAsync());
        }

        private void OnApplicationFocus(bool active)
        {
            if (game != null && !stopped) game.ActualGame.IsActive = active;
        }

        private void OnDestroy()
        {
            if (stopped) return;
            lifetime.Cancel();
            // Browser close cannot await a durable-save acknowledgement. Explicit StopAsync is the successful-close path.
            if (!stopped)
            {
                Debug.LogWarning(
                    "Web host destroyed without awaited StopAsync; durable persistence is not acknowledged.");
                _ = ReportFailureAsync(StopAsync());
            }
        }

        private void DisposeOwners()
        {
            if (stopped) return;
            stopped = true;
            lifetime.Cancel();
            try
            {
                DetachInputBridge();
            }
            finally
            {
                try
                {
                    input?.Dispose();
                }
                finally
                {
                    try
                    {
                        source?.Dispose();
                    }
                    finally
                    {
                        try
                        {
                            transport?.Dispose();
                        }
                        finally
                        {
                            try
                            {
                                graphicsAudio?.Dispose();
                            }
                            finally
                            {
                                lifetime.Dispose();
                            }
                        }
                    }
                }
            }
        }

        private async Task ReportFailureAsync(Task task)
        {
            try
            {
                await task;
            }
            catch (Exception exception)
            {
                bool abort = !closing && !stopped;
                closing = true;
                if (abort) graphicsAudio?.AbortUnsubmittedDraws();
                Debug.LogException(exception);
                try
                {
                    UnityWebBrowserInterop.NotifyGameFailed();
                }
                catch (Exception notificationException)
                {
                    Debug.LogException(notificationException);
                }
                finally
                {
                    if (stopTask == null) _ = ReportFailureAsync(StopAsync());
                }
                // Startup/Stop/Persist retain the original fault for their caller.
            }
        }

        private static Uri RequireHttp(string value)
        {
            var page = new Uri(Application.absoluteURL, UriKind.Absolute);
            var uri = new Uri(page, value);
            if (uri.Scheme != "http" && uri.Scheme != "https")
                throw new ArgumentException("An HTTP(S) resource URI is required.");
            return uri;
        }

        private static async Task<byte[]> ReadFrameworkResourcesAsync(Uri uri, CancellationToken cancellationToken)
        {
            using var request = UnityWebRequest.Get(uri.AbsoluteUri);
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Awaitable.NextFrameAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (request.result != UnityWebRequest.Result.Success)
                throw new IOException("Framework resource download failed: " + request.error);
            return request.downloadHandler.data ??
                   throw new IOException("Framework resource download returned no bytes.");
        }
    }
}
