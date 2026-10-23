"use strict";
let header;
let stage;
let index;
let source;
let stopped = false;
let pump;
const urls = new Map();
const preparations = new Map();
const scopes = new Set();
const tasks = new Set();
let progressTimer;
let lastProgress = -Infinity;
function publishProgress(force = false) {
    if (stopped || !source) return;
    const remaining = 250 - (performance.now() - lastProgress);
    if (!force && remaining > 0) {
        if (progressTimer === undefined) {
            progressTimer = setTimeout(() => {
                progressTimer = undefined;
                publishProgress(true);
            }, remaining);
        }
        return;
    }
    if (progressTimer !== undefined) {
        clearTimeout(progressTimer);
        progressTimer = undefined;
    }
    lastProgress = performance.now();
    if (stopped || !source) return;
    const pages = [];
    let readyFiles = 0;
    for (const file of scopes) {
        const entries = index.banks[file].startupPages.map((page) => [file, page]);
        pages.push(...entries);
        const state = source.progress(entries);
        if (state.readyPages === entries.length) readyFiles++;
    }
    const state = source.progress(pages);
    postMessage({ type: "progress", ...state, totalFiles: scopes.size, readyFiles });
}
function configure(message) {
    const bank = index.banks[message.fileId];
    if (!bank || bank.size !== message.size || bank.sha256.toLowerCase() !== message.sha256.toLowerCase()) {
        throw new Error("Bank identity does not match the published page index.");
    }
    const url = new URL(message.url, self.location.href).href;
    const previous = urls.get(bank.path);
    if (previous && previous !== url) throw new Error("Bank URL changed within one module.");
    urls.set(bank.path, url);
}
async function prepare(message) {
    configure(message);
    const request = { canceled: false };
    preparations.set(message.requestId, request);
    scopes.add(message.fileId);
    publishProgress(true);
    try {
        const pages = index.banks[message.fileId].startupPages;
        let next = 0;
        async function consume() {
            while (next < pages.length && !request.canceled && !stopped) {
                // Leave a pending native read the first chance to start its cache/network demand.
                if ((Atomics.load(header, 0) & 7) === 1) await new Promise((resolve) => setTimeout(resolve, 0));
                if (request.canceled || stopped) return;
                const page = pages[next++];
                if (page === undefined) return;
                await source.getPage(message.fileId, page);
            }
        }
        await Promise.all(Array.from({ length: Math.min(4, pages.length) }, consume));
        if (!request.canceled && !stopped) {
            publishProgress(true);
            postMessage({ type: "prepared", requestId: message.requestId });
        }
    } finally {
        preparations.delete(message.requestId);
    }
}
async function waitForChange(position, expected) {
    const wait = Atomics.waitAsync(header, position, expected);
    if (wait.async) await wait.value;
}
async function serve() {
    while (!stopped && Atomics.load(header, 6) === 0) await new Promise((resolve) => setTimeout(resolve, 1));
    if (stopped) return;
    postMessage({ type: "ready" });
    while (!stopped) {
        const token = Atomics.load(header, 0);
        if ((token & 7) !== 1) {
            await waitForChange(0, token);
            continue;
        }
        const file = header[1];
        const offset = header[2];
        const page = Math.floor(offset / index.pageSize);
        const within = offset % index.pageSize;
        const length = header[3];
        try {
            const bytes = await source.getPage(file, page);
            if (stopped) return;
            if (within + length > bytes.length) throw new Error("Native page length differs from page index.");
            if (Atomics.compareExchange(header, 0, token, (token & ~7) | 2) !== token) continue;
            stage.set(bytes.subarray(within, within + length));
            Atomics.store(header, 0, (token & ~7) | 3);
            Atomics.notify(header, 0);
            // A single neighboring coarse page is speculative, never part of preparation progress.
            if (within + length >= (index.pageSize * 3) / 4 && page + 1 < index.banks[file].pages.length) {
                source.getPage(file, page + 1).catch(() => {});
            }
        } catch (error) {
            if (stopped) return;
            if (Atomics.compareExchange(header, 0, token, (token & ~7) | 5) === token) Atomics.notify(header, 0);
            postMessage({ type: "readError", message: String(error) });
        }
    }
}
function failWorker(error) {
    if (header) {
        Atomics.store(header, 15, 1);
        const token = Atomics.load(header, 0);
        Atomics.store(header, 0, (token & ~7) | 5);
        Atomics.notify(header, 0);
    }
    stopped = true;
    if (progressTimer !== undefined) clearTimeout(progressTimer);
    if (source) source.dispose();
    postMessage({ type: "error", message: String(error) });
}
self.addEventListener("error", (event) => failWorker(event.error || event.message));
self.addEventListener("unhandledrejection", (event) => failWorker(event.reason));
self.onmessage = (event) => {
    const message = event.data;
    if (message.type === "cancelPrepare") {
        const request = preparations.get(message.requestId);
        if (request) request.canceled = true;
        return;
    }
    if (message.type === "stop") {
        stopped = true;
        if (progressTimer !== undefined) clearTimeout(progressTimer);
        if (source) source.dispose();
        if (header) Atomics.notify(header, 0);
        Promise.allSettled([pump, ...tasks]).then(() => postMessage({ type: "stopped" }));
        return;
    }
    const task = (async () => {
        if (stopped) throw new Error("Bank worker was stopped.");
        if (message.type === "initialize") {
            if (source) throw new Error("Bank worker is already initialized.");
            importScripts(message.cacheScriptUrl, message.pageSourceScriptUrl);
            index = message.index;
            header = new Int32Array(message.memory, message.pointer, 16);
            stage = new Uint8Array(message.memory, message.pointer + 64, index.pageSize);
            for (const bank of index.banks) {
                if (
                    !Array.isArray(bank.startupPages) ||
                    bank.startupPages.some((page) => !Number.isInteger(page) || page < 0 || page >= bank.pages.length)
                ) {
                    throw new Error("Bank startup page capture is missing or invalid.");
                }
            }
            source = createFmodBankPageSource(
                index,
                (bank) => {
                    const url = urls.get(bank.path);
                    if (!url) throw new Error("Bank URL was not prepared: " + bank.path);
                    return url;
                },
                createVerifiedContentCache,
                { progress: publishProgress },
            );
            pump = serve();
            pump.catch(failWorker);
        } else if (message.type === "configure") {
            configure(message);
            postMessage({ type: "configured", requestId: message.requestId });
        } else if (message.type === "prepare") {
            await prepare(message);
        }
    })();
    tasks.add(task);
    task.catch((error) => {
        if (!stopped) postMessage({ type: "error", requestId: message.requestId, message: String(error) });
    }).finally(() => tasks.delete(task));
};
