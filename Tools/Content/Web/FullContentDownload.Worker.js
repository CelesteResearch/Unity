"use strict";
let lifetime,
    pageSource,
    running = false,
    cancelled = false,
    timer;
let lastPublished = -Infinity;
const transfers = new Map();
let progress = {
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
function publish(force = false) {
    progress.receivedBytes = progress.readyBytes;
    for (const transfer of transfers.values()) progress.receivedBytes += transfer.received;
    progress.receivedBytes = Math.min(progress.totalBytes, progress.receivedBytes);
    const remaining = 250 - (performance.now() - lastPublished);
    if (!force && remaining > 0) {
        if (timer === undefined)
            timer = setTimeout(() => {
                timer = undefined;
                publish(true);
            }, remaining);
        return;
    }
    if (timer !== undefined) {
        clearTimeout(timer);
        timer = undefined;
    }
    lastPublished = performance.now();
    postMessage({ type: "progress", progress: { ...progress } });
}
function transferProgress(key, received, cached, downloaded) {
    let transfer = transfers.get(key);
    if (!transfer) {
        transfer = { received: 0, downloaded: 0 };
        transfers.set(key, transfer);
    }
    transfer.received = received;
    if (!cached) {
        const current = Number.isFinite(downloaded) ? downloaded : received;
        progress.downloadedBytes += Math.max(0, current - transfer.downloaded);
        transfer.downloaded = Math.max(current, transfer.downloaded);
    }
    publish();
}
function contentUrl(file, base) {
    const relative = file.ResourcePath.split("/")
        .map((segment) =>
            encodeURIComponent(segment).replace(
                /[!'()*]/g,
                (char) => "%" + char.charCodeAt(0).toString(16).toUpperCase(),
            ),
        )
        .join("/");
    return new URL(relative, base).href;
}
async function json(url) {
    const response = await fetch(url, { signal: lifetime.signal });
    if (!response.ok) throw new Error("Resource metadata HTTP " + response.status + ": " + url);
    const value = await response.json();
    lifetime.signal.throwIfAborted();
    return value;
}
function bundleMembers(manifest, files) {
    const result = new Map();
    for (const bundle of manifest.SmallResourceBundles || []) {
        if (
            !Number.isSafeInteger(bundle.Size) ||
            bundle.Size <= 0 ||
            bundle.Size > 1048576 ||
            !/^[a-f0-9]{64}$/i.test(bundle.Sha256) ||
            bundle.ResourcePath !== "bundles/" + bundle.Sha256.toLowerCase() + ".data" ||
            !Array.isArray(bundle.Files) ||
            bundle.Files.length < 2 ||
            bundle.Files.length > 32
        )
            throw new Error("Invalid small-resource bundle.");
        let offset = 0;
        for (const member of bundle.Files) {
            const file = files.get(member.Path);
            if (
                !file ||
                file.Size <= 0 ||
                file.Size >= 65536 ||
                file.Path.toLowerCase().endsWith(".bank") ||
                member.Offset !== offset ||
                result.has(member.Path)
            )
                throw new Error("Invalid small-resource bundle member.");
            result.set(member.Path, { bundle, offset });
            offset += file.Size;
        }
        if (offset !== bundle.Size) throw new Error("Small-resource bundle size mismatch.");
    }
    return result;
}
async function readBundle(bundle, settings) {
    const response = await fetch(contentUrl(bundle, settings.contentBaseUrl), { signal: lifetime.signal });
    if (!response.ok || !response.body) throw new Error("Small-resource bundle HTTP " + response.status);
    const reader = response.body.getReader();
    const bytes = new Uint8Array(bundle.Size);
    const header = response.headers.get("Content-Length");
    const encoded = header === null ? NaN : Number(header);
    const scale = Number.isSafeInteger(encoded) && encoded > 0 ? encoded / bundle.Size : 0;
    let received = 0,
        complete = false;
    try {
        while (true) {
            lifetime.signal.throwIfAborted();
            const part = await reader.read();
            if (part.done) {
                complete = true;
                break;
            }
            if (received + part.value.byteLength > bytes.byteLength)
                throw new Error("Small-resource bundle exceeds its size.");
            bytes.set(part.value, received);
            received += part.value.byteLength;
            // Decoded Fetch chunks are not wire bytes. Unknown encoded lengths contribute no invented rate.
            progress.downloadedBytes += part.value.byteLength * scale;
            publish();
        }
        if (received !== bytes.byteLength) throw new Error("Small-resource bundle is incomplete.");
        const hash = Array.from(new Uint8Array(await crypto.subtle.digest("SHA-256", bytes)), (value) =>
            value.toString(16).padStart(2, "0"),
        ).join("");
        if (hash !== bundle.Sha256.toLowerCase()) throw new Error("Small-resource bundle SHA-256 mismatch.");
        return bytes;
    } finally {
        if (!complete) {
            try {
                await reader.cancel();
            } catch {}
        }
        reader.releaseLock();
    }
}
async function run(settings) {
    running = true;
    lifetime = new AbortController();
    progress.state = "Running";
    publish(true);
    let failure;
    try {
        importScripts(settings.cacheScriptUrl, settings.pageSourceScriptUrl);
        const [manifest, index] = await Promise.all([json(settings.manifestUrl), json(settings.bankIndexUrl)]);
        if (manifest.SchemaVersion !== 1 || !Array.isArray(manifest.Files))
            throw new Error("Unsupported resource manifest.");
        const files = new Map();
        for (const file of manifest.Files) {
            if (
                typeof file.Path !== "string" ||
                typeof file.ResourcePath !== "string" ||
                file.ResourcePath.split("/").some((part) => !part || part === "." || part === "..") ||
                !Number.isSafeInteger(file.Size) ||
                file.Size < 0 ||
                !/^[a-f0-9]{64}$/i.test(file.Sha256) ||
                files.has(file.Path)
            )
                throw new Error("Invalid resource manifest entry.");
            files.set(file.Path, file);
        }
        const banks = new Map();
        for (let id = 0; id < index.banks.length; id++) {
            const bank = index.banks[id];
            const file = files.get(bank.path);
            if (
                !file ||
                bank.size !== file.Size ||
                bank.sha256.toLowerCase() !== file.Sha256.toLowerCase() ||
                banks.has(bank.path)
            )
                throw new Error("Bank page index does not match the complete resource manifest.");
            banks.set(bank.path, id);
        }
        for (const file of manifest.Files)
            if (file.Path.toLowerCase().endsWith(".bank") && !banks.has(file.Path))
                throw new Error("A manifest bank is missing its page index.");
        let members;
        try {
            members = bundleMembers(manifest, files);
        } catch {
            members = new Map();
        }
        const selectedBundles = new Map();
        const bundledRequests = new Set();
        const bundleReads = new Set();
        async function loadBundle(bundle) {
            while (bundleReads.size >= 2) {
                await Promise.race(bundleReads);
                lifetime.signal.throwIfAborted();
            }
            let release;
            const reservation = new Promise((resolve) => {
                release = resolve;
            });
            bundleReads.add(reservation);
            try {
                return await readBundle(bundle, settings);
            } finally {
                bundleReads.delete(reservation);
                release();
            }
        }
        const cache = createVerifiedContentCache({
            fetch: async (url, options) => {
                const member = bundledRequests.has(url) ? selectedBundles.get(url) : null;
                if (!member) return fetch(url, options);
                const state = member.state;
                state.pending ||= loadBundle(state.bundle).catch(() => {
                    lifetime.signal.throwIfAborted();
                    return null;
                });
                const bytes = await state.pending;
                let payload;
                if (bytes) {
                    payload = bytes.subarray(member.offset, member.offset + member.size);
                    const hash = Array.from(new Uint8Array(await crypto.subtle.digest("SHA-256", payload)), (value) =>
                        value.toString(16).padStart(2, "0"),
                    ).join("");
                    if (hash !== member.sha256) payload = null;
                }
                if (!payload) {
                    bundledRequests.delete(url);
                    return fetch(url, options);
                }
                return new Response(payload);
            },
        });
        pageSource = createFmodBankPageSource(
            index,
            (bank) => contentUrl(files.get(bank.path), settings.contentBaseUrl),
            createVerifiedContentCache,
            {
                retainBytes: false,
                requirePersistence: true,
                signal: lifetime.signal,
                transferProgress: (bank, page, received, cached, downloaded) =>
                    transferProgress("bank/" + bank + "/" + page, received, cached, downloaded),
            },
        );
        const jobs = [],
            remainingPages = index.banks.map((bank) => bank.pages.length);
        progress.totalFiles = manifest.Files.length;
        for (const file of manifest.Files) {
            progress.totalBytes += file.Size;
            const bank = banks.get(file.Path);
            if (bank === undefined) jobs.push({ file });
            else if (remainingPages[bank] === 0) progress.readyFiles++;
            else for (let page = 0; page < remainingPages[bank]; page++) jobs.push({ file, bank, page });
        }
        const ordinary = [],
            small = [];
        for (const job of jobs) {
            if (job.bank === undefined && members.has(job.file.Path)) small.push(job);
            else ordinary.push(job);
        }
        const missing = new Map();
        let probe = 0;
        async function checkCache() {
            while (probe < small.length) {
                const job = small[probe++];
                const file = job.file;
                const cached = await cache.readOwned(
                    contentUrl(file, settings.contentBaseUrl),
                    file.Size,
                    file.Sha256,
                    { requirePersistence: true, signal: lifetime.signal, cacheOnly: true },
                );
                if (cached) {
                    progress.readyBytes += file.Size;
                    progress.cachedBytes += file.Size;
                    progress.readyFiles++;
                    publish();
                } else missing.set(file.Path, job);
            }
        }
        await Promise.all(Array.from({ length: 6 }, checkCache));
        const grouped = new Map();
        for (const [path, job] of missing) {
            const member = members.get(path);
            let group = grouped.get(member.bundle);
            if (!group) grouped.set(member.bundle, (group = []));
            group.push(job);
        }
        const bundledJobs = [];
        for (const [bundle, group] of grouped) {
            const needed = group.reduce((sum, job) => sum + job.file.Size, 0);
            if (group.length < 2 || needed < bundle.Size / 2) {
                ordinary.push(...group);
                continue;
            }
            const state = { bundle, remaining: group.length, pending: null };
            for (const job of group) {
                const url = contentUrl(job.file, settings.contentBaseUrl);
                selectedBundles.set(url, {
                    state,
                    offset: members.get(job.file.Path).offset,
                    size: job.file.Size,
                    sha256: job.file.Sha256.toLowerCase(),
                });
                bundledRequests.add(url);
                bundledJobs.push(job);
            }
        }
        jobs.splice(0, jobs.length, ...bundledJobs, ...ordinary);
        publish(true);
        let next = 0;
        const largeReads = new Set();
        async function readFile(file) {
            // Small files/pages can overlap their request latency; keep large atlas buffers bounded.
            const large = file.Size > 4 * 1024 * 1024;
            const url = contentUrl(file, settings.contentBaseUrl);
            let released, reservation;
            if (large) {
                while (largeReads.size >= 2) {
                    await Promise.race(largeReads);
                    lifetime.signal.throwIfAborted();
                }
                reservation = new Promise((resolve) => {
                    released = resolve;
                });
                largeReads.add(reservation);
            }
            try {
                return await cache.readOwned(url, file.Size, file.Sha256, {
                    requirePersistence: true,
                    signal: lifetime.signal,
                    progress: (received, cached, downloaded) =>
                        transferProgress(file.Path, received, cached, bundledRequests.has(url) ? 0 : downloaded),
                });
            } finally {
                const member = selectedBundles.get(url);
                if (member && --member.state.remaining === 0) member.state.pending = null;
                if (reservation) {
                    largeReads.delete(reservation);
                    released();
                }
            }
        }
        async function consume() {
            try {
                while (next < jobs.length) {
                    lifetime.signal.throwIfAborted();
                    const job = jobs[next++];
                    progress.currentPath = job.file.Path;
                    publish();
                    let size, cached;
                    if (job.bank === undefined) {
                        const result = await readFile(job.file);
                        size = job.file.Size;
                        cached = result.cached;
                        progress.readyFiles++;
                    } else {
                        const bytes = await pageSource.getPage(job.bank, job.page);
                        size = bytes.byteLength;
                        cached = pageSource.progress([[job.bank, job.page]]).cachedBytes === size;
                        if (--remainingPages[job.bank] === 0) progress.readyFiles++;
                    }
                    lifetime.signal.throwIfAborted();
                    progress.readyBytes += size;
                    if (cached) progress.cachedBytes += size;
                    transfers.delete(job.bank === undefined ? job.file.Path : "bank/" + job.bank + "/" + job.page);
                    publish();
                }
            } catch (error) {
                if (!failure && !cancelled) failure = error;
                lifetime.abort(error);
                throw error;
            }
        }
        // Six bounded consumers, at most two large files; no whole-bank buffer or live FMOD module.
        await Promise.allSettled(Array.from({ length: 6 }, consume));
        if (failure) throw failure;
        lifetime.signal.throwIfAborted();
        progress.state = "Completed";
        progress.currentPath = "";
    } catch (error) {
        progress.state = cancelled && !failure ? "Cancelled" : "Failed";
        progress.error = progress.state === "Failed" ? String(error) : "";
    } finally {
        if (pageSource) pageSource.dispose();
        if (timer !== undefined) {
            clearTimeout(timer);
            timer = undefined;
        }
        running = false;
        postMessage({ type: "settled", progress: { ...progress } });
    }
}
self.onmessage = (event) => {
    if (event.data.type === "cancel") {
        cancelled = true;
        if (lifetime) lifetime.abort();
        return;
    }
    if (event.data.type === "start" && !running) run(event.data.settings);
};
