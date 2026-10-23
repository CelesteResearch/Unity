mergeInto(LibraryManager.library, {
    $CelesteFullContentDownloads: {},
    CelesteWebDownloadCreate__deps: [
        "$CelesteFullContentDownloads",
        "$CelesteFullContentDownloadFactory",
        "$UTF8ToString",
    ],
    CelesteWebDownloadCreate: function (id, manifestUrl, contentUrl) {
        var manifest = UTF8ToString(manifestUrl);
        CelesteFullContentDownloads[id] = CelesteFullContentDownloadFactory({
            manifestUrl: manifest,
            contentBaseUrl: UTF8ToString(contentUrl),
            workerUrl: new URL("Cache/FullContentDownload.Worker.js", manifest).href,
            bankIndexUrl: new URL("FMOD/2.03.15/OriginalBankPages.json", manifest).href,
            cacheScriptUrl: new URL("FMOD/2.03.15/VerifiedContentCache.js", manifest).href,
            pageSourceScriptUrl: new URL("FMOD/2.03.15/FmodBankPageSource.js", manifest).href,
        });
    },
    CelesteWebDownloadStart__deps: ["$CelesteFullContentDownloads"],
    CelesteWebDownloadStart: function (id) {
        CelesteFullContentDownloads[id].start();
    },
    CelesteWebDownloadCancel__deps: ["$CelesteFullContentDownloads"],
    CelesteWebDownloadCancel: function (id) {
        var owner = CelesteFullContentDownloads[id];
        if (owner) owner.cancel();
    },
    CelesteWebDownloadRead__deps: [
        "$CelesteFullContentDownloads",
        "$lengthBytesUTF8",
        "$stringToUTF8",
        "malloc",
        "free",
    ],
    CelesteWebDownloadRead: function (id, callback) {
        var state = CelesteFullContentDownloads[id].readProgress();
        var states = ["Idle", "Running", "Completed", "Cancelled", "Failed"];
        var path = 0,
            error = 0;
        function text(value) {
            if (!value) return 0;
            var size = lengthBytesUTF8(value) + 1;
            var pointer = _malloc(size);
            if (!pointer) throw new Error("Resource progress text allocation failed.");
            stringToUTF8(value, pointer, size);
            return pointer;
        }
        try {
            path = text(state.currentPath);
            error = text(state.error);
            {{{ makeDynCall('viidddddiiii', 'callback') }}}(
                id,
                states.indexOf(state.state),
                state.readyBytes,
                state.totalBytes,
                state.cachedBytes,
                state.receivedBytes || 0,
                state.downloadedBytes || 0,
                state.readyFiles,
                state.totalFiles,
                path,
                error,
            );
        } finally {
            if (path) _free(path);
            if (error) _free(error);
        }
    },
    CelesteWebDownloadDispose__deps: ["$CelesteFullContentDownloads"],
    CelesteWebDownloadDispose: function (id) {
        var owner = CelesteFullContentDownloads[id];
        delete CelesteFullContentDownloads[id];
        if (owner) owner.dispose();
    },
});
