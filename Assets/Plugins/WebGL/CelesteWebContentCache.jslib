mergeInto(LibraryManager.library, {
    $CelesteWebContentRequests: {},
    CelesteWebPrepareContentBlob__deps: [
        "$CelesteWebContentRequests",
        "$CelesteVerifiedContentCache",
        "$UTF8ToString",
        "$stringToUTF8",
        "$lengthBytesUTF8",
        "malloc",
        "free",
    ],
    CelesteWebPrepareContentBlob: function (request, url, size, sha256, completion, progress) {
        var state = { controller: new AbortController(), url: null };
        CelesteWebContentRequests[request] = state;
        var resourceUrl = UTF8ToString(url);
        var digest = UTF8ToString(sha256);
        function current() {
            return CelesteWebContentRequests[request] === state && !state.controller.signal.aborted;
        }
        function complete(error, text, cached) {
            var pointer = _malloc(lengthBytesUTF8(text) + 1);
            try {
                stringToUTF8(text, pointer, lengthBytesUTF8(text) + 1);
                {{{ makeDynCall('viiiii', 'completion') }}}(
                    request,
                    error,
                    error ? 0 : pointer,
                    error ? pointer : 0,
                    cached ? 1 : 0,
                );
            } finally {
                _free(pointer);
            }
        }
        Promise.resolve()
            .then(function () {
                if (!current()) return null;
                return CelesteVerifiedContentCache.readOwned(resourceUrl, size, digest, {
                    signal: state.controller.signal,
                    progress: function (received, cached) {
                        if (current()) {{{ makeDynCall('vidi', 'progress') }}}(request, received, cached ? 1 : 0);
                    },
                });
            })
            .then(
                function (result) {
                    if (!current()) return;
                    try {
                        state.url = URL.createObjectURL(new Blob([result.bytes], { type: "application/octet-stream" }));
                    } catch (error) {
                        complete(1, error && error.message ? error.message : String(error), false);
                        return;
                    }
                    complete(0, state.url, result.cached);
                },
                function (error) {
                    if (current()) complete(1, error && error.message ? error.message : String(error), false);
                },
            );
    },
    CelesteWebCancelContentBlob__deps: ["$CelesteWebContentRequests"],
    CelesteWebCancelContentBlob: function (request) {
        var state = CelesteWebContentRequests[request];
        if (!state) return;
        delete CelesteWebContentRequests[request];
        state.controller.abort();
        if (state.url) URL.revokeObjectURL(state.url);
    },
    CelesteWebReleaseContentBlob__deps: ["CelesteWebCancelContentBlob"],
    CelesteWebReleaseContentBlob: function (request) {
        _CelesteWebCancelContentBlob(request);
    },
});
