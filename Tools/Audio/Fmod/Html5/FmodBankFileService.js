(function (root, factory) {
    if (typeof module === "object" && module && module.exports) module.exports = factory;
    else root.createFmodBankFileService = factory;
})(typeof globalThis !== "undefined" ? globalThis : this, async function (fmod, scriptUrl, index, progress) {
    "use strict";
    const result = fmod._CelesteBankIO_Start();
    if (result !== fmod.OK) throw new Error("FMOD bank service start: " + fmod.ErrorString(result));
    let worker;
    let closing;
    let failure;
    let started = false;
    let readyResolve, readyReject, stopResolve, stopReject;
    const pending = new Map();
    const ready = new Promise((resolve, reject) => {
        readyResolve = resolve;
        readyReject = reject;
    });
    function fail(error) {
        failure = error;
        let stoppedSafely = false;
        if (started && worker) {
            worker.terminate();
            worker = null;
            const failed = fmod._CelesteBankIO_Fail();
            if (failed !== fmod.OK) failure = new Error("FMOD bank service failure: " + fmod.ErrorString(failed));
            else stoppedSafely = true;
        }
        readyReject(error);
        if (stopResolve && stoppedSafely) stopResolve();
        else if (stopReject) stopReject(failure);
        for (const request of pending.values()) request.reject(error);
        pending.clear();
    }
    function stopNative() {
        const stopped = fmod._CelesteBankIO_Stop();
        if (stopped !== fmod.OK) throw new Error("FMOD bank service stop: " + fmod.ErrorString(stopped));
    }
    function close() {
        if (closing) return closing;
        closing = (async () => {
            for (const request of pending.values()) request.reject(new Error("FMOD bank service was disposed."));
            pending.clear();
            if (worker) {
                const stopped = new Promise((resolve, reject) => {
                    stopResolve = resolve;
                    stopReject = reject;
                });
                worker.postMessage({ type: "stop" });
                await stopped;
                if (worker) {
                    worker.terminate();
                    worker = null;
                }
            }
            stopNative();
        })();
        return closing;
    }
    try {
        for (let i = 0; i < index.banks.length; i++) {
            const configured = fmod._CelesteBankIO_SetFile(i, index.banks[i].size);
            if (configured !== fmod.OK) throw new Error("FMOD bank registration: " + fmod.ErrorString(configured));
        }
        worker = new Worker(new URL("FmodBankRangeWorker.js", scriptUrl).href);
        worker.onmessage = (event) => {
            const message = event.data;
            switch (message.type) {
                case "ready":
                    started = true;
                    readyResolve();
                    break;
                case "stopped":
                    if (stopResolve) stopResolve();
                    break;
                case "progress":
                    progress(message);
                    break;
                case "readError":
                    console.error("FMOD bank read: " + message.message);
                    break;
                case "prepared": {
                    const request = pending.get(message.requestId);
                    if (request) {
                        pending.delete(message.requestId);
                        request.resolve();
                    }
                    break;
                }
                case "error": {
                    const error = new Error(message.message);
                    const request = pending.get(message.requestId);
                    if (request) {
                        pending.delete(message.requestId);
                        request.reject(error);
                    } else if (message.requestId === undefined) fail(error);
                    break;
                }
            }
        };
        worker.onerror = (event) => fail(new Error(event.message || "FMOD bank worker failed."));
        worker.postMessage({
            type: "initialize",
            memory: fmod.wasmMemory.buffer,
            pointer: fmod._CelesteBankIO_GetExchange(),
            index,
            cacheScriptUrl: new URL("VerifiedContentCache.js", scriptUrl).href,
            pageSourceScriptUrl: new URL("FmodBankPageSource.js", scriptUrl).href,
        });
        await ready;
    } catch (error) {
        // No Studio can exist yet: the native service has no submitted file requests.
        if (worker) worker.terminate();
        stopNative();
        throw error;
    }
    return {
        prepare(requestId, fileId, url, size, sha256) {
            if (failure) return Promise.reject(failure);
            if (closing) return Promise.reject(new Error("FMOD bank service was disposed."));
            return new Promise((resolve, reject) => {
                pending.set(requestId, { resolve, reject });
                worker.postMessage({ type: "prepare", requestId, fileId, url, size, sha256 });
            });
        },
        cancel(requestId) {
            const request = pending.get(requestId);
            if (!request) return;
            pending.delete(requestId);
            worker.postMessage({ type: "cancelPrepare", requestId });
            request.reject(new Error("FMOD bank preparation was cancelled."));
        },
        close,
    };
});
