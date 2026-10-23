(function (root, factory) {
    "use strict";
    if (typeof module === "object" && module && module.exports) module.exports = factory;
    else root.createFullContentDownload = factory;
})(typeof globalThis !== "undefined" ? globalThis : this, function (settings) {
    "use strict";
    const names = [
        "manifestUrl",
        "contentBaseUrl",
        "bankIndexUrl",
        "cacheScriptUrl",
        "pageSourceScriptUrl",
        "workerUrl",
    ];
    const configuration = {};
    for (const name of names) {
        if (typeof settings[name] !== "string" || !settings[name])
            throw new Error("Resource download URL is missing: " + name);
        configuration[name] = new URL(settings[name], globalThis.location.href).href;
    }
    let snapshot = {
        state: "Idle",
        readyBytes: 0,
        totalBytes: 0,
        cachedBytes: 0,
        receivedBytes: 0,
        downloadedBytes: 0,
        readyFiles: 0,
        totalFiles: 0,
        currentPath: "",
        error: "",
    };
    let worker,
        promise,
        resolve,
        disposed = false;
    function readProgress() {
        return { ...snapshot };
    }
    function finish(value) {
        snapshot = value;
        if (worker) {
            worker.onmessage = null;
            worker.onerror = null;
            worker.terminate();
            worker = null;
        }
        const complete = resolve;
        resolve = null;
        if (complete) complete(readProgress());
    }
    function start() {
        if (disposed) throw new Error("Full resource download was disposed.");
        if (worker) return promise;
        snapshot = {
            state: "Running",
            readyBytes: 0,
            totalBytes: 0,
            cachedBytes: 0,
            receivedBytes: 0,
            downloadedBytes: 0,
            readyFiles: 0,
            totalFiles: 0,
            currentPath: "",
            error: "",
        };
        promise = new Promise((complete) => {
            resolve = complete;
        });
        try {
            worker = new Worker(configuration.workerUrl);
            worker.onmessage = (event) => {
                if (event.data.type === "progress") snapshot = event.data.progress;
                else if (event.data.type === "settled") finish(event.data.progress);
            };
            worker.onerror = (event) => {
                if (event.preventDefault) event.preventDefault();
                finish({ ...snapshot, state: "Failed", error: String(event.message || "Resource worker failed.") });
            };
            worker.postMessage({ type: "start", settings: configuration });
        } catch (error) {
            finish({ ...snapshot, state: "Failed", error: String(error.message || error) });
        }
        return promise;
    }
    function cancel() {
        if (worker) worker.postMessage({ type: "cancel" });
    }
    function dispose() {
        if (disposed) return;
        disposed = true;
        if (worker) finish({ ...snapshot, state: "Cancelled", error: "" });
    }
    return { start, cancel, readProgress, dispose };
});
