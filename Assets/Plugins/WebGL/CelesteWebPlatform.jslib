mergeInto(LibraryManager.library, {
    $CelesteFullscreen: {
        pending: null,
        enabled: false,
        clear: function () {
            if (!CelesteFullscreen.pending) return;
            document.removeEventListener("keydown", CelesteFullscreen.pending, true);
            document.removeEventListener("pointerup", CelesteFullscreen.pending, true);
            document.removeEventListener("touchend", CelesteFullscreen.pending, true);
            CelesteFullscreen.pending = null;
        },
        attempt: function (target, method) {
            try {
                var result = method.call(target);
                if (result && typeof result.then === "function")
                    result.then(
                        function () {
                            // A pending entry may finish after the user has switched fullscreen off.
                            if (!CelesteFullscreen.enabled && target !== document) CelesteFullscreen.set(false);
                            else if (CelesteFullscreen.enabled && target === document) CelesteFullscreen.set(true);
                        },
                        function () {
                            /* Fullscreen refusal must not interrupt the game. */
                        },
                    );
            } catch (_) {
                /* Missing permission or browser support is nonfatal. */
            }
        },
        set: function (enabled) {
            CelesteFullscreen.enabled = enabled;
            CelesteFullscreen.clear();
            var current = document.fullscreenElement || document.webkitFullscreenElement;
            if (!enabled) {
                var exit = document.exitFullscreen || document.webkitExitFullscreen;
                if (current && exit) CelesteFullscreen.attempt(document, exit);
                return;
            }
            if (current) return;
            var target = document.getElementById(Module.fullscreenElementID || "celeste-viewport");
            if (!target) return;
            var request = target.requestFullscreen || target.webkitRequestFullscreen;
            if (!request || document.fullscreenEnabled === false || document.webkitFullscreenEnabled === false) return;
            function enter(event) {
                if (!event.isTrusted) return;
                if (event.key === "Escape" || (navigator.userActivation && !navigator.userActivation.isActive)) return;
                CelesteFullscreen.clear();
                CelesteFullscreen.attempt(target, request);
            }
            // The game changes settings during its frame, outside the original input callback.
            if (navigator.userActivation && navigator.userActivation.isActive)
                CelesteFullscreen.attempt(target, request);
            else {
                CelesteFullscreen.pending = enter;
                document.addEventListener("keydown", enter, true);
                document.addEventListener("pointerup", enter, true);
                document.addEventListener("touchend", enter, true);
            }
        },
    },
    CelesteWebSetFullscreen__deps: ["$CelesteFullscreen"],
    CelesteWebSetFullscreen: function (enabled) {
        CelesteFullscreen.set(!!enabled);
    },
    CelesteWebSelectSaveFile__deps: ["$FS", "$UTF8ToString"],
    CelesteWebSelectSaveFile: function (path, callback) {
        var target = UTF8ToString(path);
        var input = document.createElement("input");
        input.type = "file";
        input.accept = ".celeste,.xml";
        input.style.display = "none";
        document.body.appendChild(input);
        var completed = false;
        function finish(result) {
            if (completed) return;
            completed = true;
            input.remove();
            {{{ makeDynCall('vii', 'callback') }}}(0, result);
        }
        input.addEventListener("cancel", function () {
            finish(0);
        });
        input.addEventListener("change", function () {
            var file = input.files[0];
            if (!file) {
                finish(0);
                return;
            }
            if (file.size === 0 || file.size > 16 * 1024 * 1024) {
                finish(-1);
                return;
            }
            file.arrayBuffer().then(
                function (bytes) {
                    try {
                        FS.writeFile(target, new Uint8Array(bytes));
                        finish(1);
                    } catch (error) {
                        console.error("Celeste import failed", error);
                        finish(-1);
                    }
                },
                function () {
                    finish(-1);
                },
            );
        });
        try {
            input.click();
        } catch (error) {
            finish(-1);
        }
    },
    CelesteWebDownloadSaveFile__deps: ["$UTF8ToString"],
    CelesteWebDownloadSaveFile: function (name, bytes, count) {
        var url = URL.createObjectURL(
            new Blob([HEAPU8.slice(bytes, bytes + count)], { type: "application/octet-stream" }),
        );
        var link = document.createElement("a");
        link.href = url;
        link.download = UTF8ToString(name);
        document.body.appendChild(link);
        link.click();
        link.remove();
        setTimeout(function () {
            URL.revokeObjectURL(url);
        }, 60000);
    },
    CelesteWebConfirmSaveImport__deps: ["$UTF8ToString"],
    CelesteWebConfirmSaveImport: function (message) {
        return window.confirm(UTF8ToString(message)) ? 1 : 0;
    },
    CelesteWebReloadPage: function () {
        window.location.reload();
    },
    CelesteWebShowSaveError__deps: ["$UTF8ToString"],
    CelesteWebShowSaveError: function (message) {
        window.alert(UTF8ToString(message));
    },
    CelesteWebInstallSteamLink__deps: ["$UTF8ToString"],
    CelesteWebInstallSteamLink: function (url) {
        var previous = Module.celesteSteamLink;
        if (previous) previous.canvas.removeEventListener("pointerdown", previous.listener, false);
        Module.celesteSteamLink = null;
        var link = {
            canvas: Module.canvas,
            url: UTF8ToString(url),
            left: 0,
            top: 0,
            width: 0,
            height: 0,
            listener: null,
        };
        link.listener = function (event) {
            if (!event.isTrusted || event.button !== 0 || link.width <= 0 || link.height <= 0) return;
            var rect = link.canvas.getBoundingClientRect();
            if (rect.width <= 0 || rect.height <= 0) return;
            var x = (event.clientX - rect.left) / rect.width;
            var y = (event.clientY - rect.top) / rect.height;
            if (x >= link.left && x < link.left + link.width && y >= link.top && y < link.top + link.height)
                window.open(link.url, "_blank", "noopener,noreferrer");
        };
        link.canvas.addEventListener("pointerdown", link.listener, false);
        Module.celesteSteamLink = link;
    },
    CelesteWebSetSteamLinkRegion: function (left, top, width, height) {
        var link = Module.celesteSteamLink;
        if (!link) return;
        link.left = left;
        link.top = top;
        link.width = width;
        link.height = height;
    },
    CelesteWebRemoveSteamLink: function () {
        var link = Module.celesteSteamLink;
        if (!link) return;
        link.width = 0;
        link.height = 0;
        link.canvas.removeEventListener("pointerdown", link.listener, false);
        Module.celesteSteamLink = null;
    },
    CelesteWebReportContentProgress: function (
        totalBytes,
        receivedBytes,
        readyBytes,
        totalFiles,
        readyFiles,
        cachedBytes,
    ) {
        window.dispatchEvent(
            new CustomEvent("celeste-content-progress", {
                detail: {
                    category: "content",
                    totalBytes: totalBytes,
                    receivedBytes: receivedBytes,
                    readyBytes: readyBytes,
                    totalFiles: totalFiles,
                    readyFiles: readyFiles,
                    cachedBytes: cachedBytes,
                },
            }),
        );
    },
    CelesteWebNotifyGameReady: function () {
        window.dispatchEvent(new Event("celeste-game-ready"));
    },
    CelesteWebReportStartupStage__deps: ["$UTF8ToString"],
    CelesteWebReportStartupStage: function (stage) {
        window.dispatchEvent(new CustomEvent("celeste-startup-stage", { detail: { stage: UTF8ToString(stage) } }));
    },
    CelesteWebNotifyGameFailed: function () {
        window.dispatchEvent(new Event("celeste-game-failed"));
    },
    CelesteWebCloseGamePage__deps: ["$CelesteFullscreen"],
    CelesteWebCloseGamePage: function () {
        CelesteFullscreen.set(false);
        try {
            window.close();
        } finally {
            window.dispatchEvent(new Event("celeste-game-exited"));
        }
    },
    CelesteWebSyncPersistentFiles__deps: ["$FS", "$IDBFS"],
    CelesteWebSyncPersistentFiles: function (populate, request, callback) {
        if (Module.autoSyncPersistentDataPath) {
            console.error("Celeste explicit durable-save completion requires autoSyncPersistentDataPath=false.");
            {{{ makeDynCall('vii', 'callback') }}}(request, 1);
            return;
        }
        FS.syncfs(!!populate, function (error) {
            if (error) console.error("Celeste IndexedDB sync failed", error);
            {{{ makeDynCall('vii', 'callback') }}}(request, error ? 1 : 0);
        });
    },
    CelesteWebSetTitle: function (title) {
        document.title = UTF8ToString(title);
    },
    CelesteWebFillEntropy: function (bytes, count) {
        if (!globalThis.crypto || !globalThis.crypto.getRandomValues) return 1;
        for (var offset = 0; offset < count; offset += 65536)
            globalThis.crypto.getRandomValues(HEAPU8.subarray(bytes + offset, bytes + Math.min(count, offset + 65536)));
        return 0;
    },
});
