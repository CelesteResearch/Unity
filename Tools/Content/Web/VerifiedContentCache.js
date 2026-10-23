(function (root, factory) {
    "use strict";
    if (typeof module === "object" && module && module.exports) module.exports = factory;
    else root.createVerifiedContentCache = factory;
})(typeof globalThis !== "undefined" ? globalThis : this, function (settings) {
    "use strict";
    settings = settings || {};
    const storage = settings.cacheStorage === undefined ? globalThis.caches : settings.cacheStorage;
    const fetchContent = settings.fetch || ((url, options) => fetch(url, options));
    const cryptoProvider = settings.crypto || globalThis.crypto;
    const ResponseType = settings.Response || globalThis.Response;
    const origin = settings.origin || (globalThis.location && globalThis.location.origin) || "https://celeste.invalid";
    const chunkSize = 4 * 1024 * 1024;
    let opened;
    let notified = false;

    function cacheUnavailable() {
        if (notified) return;
        notified = true;
        (settings.info || console.info)(
            "Celeste persistent resource cache is unavailable; continuing with original downloads.",
        );
    }
    function checkCancelled(signal) {
        if (signal && signal.aborted)
            throw signal.reason || new DOMException("Resource preparation was canceled.", "AbortError");
    }
    function open() {
        if (opened === undefined) {
            if (!storage) {
                cacheUnavailable();
                opened = Promise.resolve(null);
            } else
                opened = storage.open("celeste-verified-content-v1").catch(() => {
                    cacheUnavailable();
                    return null;
                });
        }
        return opened;
    }
    async function digest(bytes) {
        const value = new Uint8Array(await cryptoProvider.subtle.digest("SHA-256", bytes));
        let result = "";
        for (const byte of value) result += byte.toString(16).padStart(2, "0");
        return result;
    }
    async function removeMarker(cache, marker) {
        try {
            await cache.delete(marker);
        } catch {
            cacheUnavailable();
        }
    }

    async function readOwned(url, size, sha256, options) {
        options = options || {};
        const signal = options.signal;
        const progress = options.progress || (() => {});
        if (!Number.isSafeInteger(size) || size < 0 || !/^[a-f0-9]{64}$/i.test(sha256))
            throw new Error("Invalid resource manifest metadata.");
        sha256 = sha256.toLowerCase();
        checkCancelled(signal);
        const cache = await open();
        checkCancelled(signal);
        if (!cache && options.requirePersistence) throw new Error("Persistent resource cache is unavailable.");
        const base = origin + "/__celeste_resource_cache/v1/" + sha256 + "/" + size + "/";
        const marker = base + "complete";
        const chunkCount = Math.ceil(size / chunkSize);
        let bytes;
        const ownedBytes = () => bytes || (bytes = new Uint8Array(size));

        if (cache) {
            let found = false;
            let allowCacheFallback = true;
            try {
                const complete = await cache.match(marker);
                checkCancelled(signal);
                if (complete) {
                    found = true;
                    const identity = await complete.json();
                    checkCancelled(signal);
                    if (
                        identity.sha256 !== sha256 ||
                        identity.size !== size ||
                        identity.chunkSize !== chunkSize ||
                        identity.chunkCount !== chunkCount
                    )
                        throw new Error("Cached resource identity mismatch.");
                    allowCacheFallback = false;
                    const destination = ownedBytes();
                    allowCacheFallback = true;
                    let received = 0;
                    for (let index = 0; index < chunkCount; index++) {
                        const part = await cache.match(base + "chunk/" + index);
                        checkCancelled(signal);
                        if (!part) throw new Error("Cached resource is incomplete.");
                        const chunk = new Uint8Array(await part.arrayBuffer());
                        checkCancelled(signal);
                        const expected = Math.min(chunkSize, size - received);
                        if (chunk.byteLength !== expected) throw new Error("Cached resource chunk size mismatch.");
                        allowCacheFallback = false;
                        destination.set(chunk, received);
                        received += chunk.byteLength;
                        progress(received, true);
                        allowCacheFallback = true;
                    }
                    allowCacheFallback = false;
                    const actual = await digest(destination);
                    allowCacheFallback = true;
                    if (actual !== sha256) throw new Error("Cached resource SHA-256 mismatch.");
                    checkCancelled(signal);
                    return { bytes: destination, cached: true };
                }
            } catch (error) {
                if (!allowCacheFallback) throw error;
                checkCancelled(signal);
                if (found) await removeMarker(cache, marker);
                else cacheUnavailable();
            }
        }

        if (options.cacheOnly) return null;
        checkCancelled(signal);
        const response = await fetchContent(url, { signal });
        checkCancelled(signal);
        if (!response.ok) throw new Error("Resource HTTP " + response.status + ": " + url);
        if (!response.body || typeof response.body.getReader !== "function")
            throw new Error("Streaming resource downloads are unavailable.");
        const destination = ownedBytes();
        const reader = response.body.getReader();
        const encodedSize = Number(response.headers && response.headers.get("Content-Length"));
        // Fetch exposes decoded chunks; scale progress to the encoded length when the server supplies it.
        const wireScale = encodedSize > 0 && size > 0 ? encodedSize / size : 1;
        let received = 0,
            storedChunks = 0;
        let ended = false,
            persist = cache !== null;
        async function storeChunk(end) {
            if (!persist) return;
            const start = storedChunks * chunkSize;
            try {
                await cache.put(base + "chunk/" + storedChunks, new ResponseType(destination.subarray(start, end)));
                storedChunks++;
            } catch (error) {
                persist = false;
                cacheUnavailable();
                await removeMarker(cache, marker);
                if (options.requirePersistence) throw error;
            }
            checkCancelled(signal);
        }
        try {
            progress(0, false, 0);
            while (true) {
                checkCancelled(signal);
                const chunk = await reader.read();
                checkCancelled(signal);
                if (chunk.done) {
                    ended = true;
                    break;
                }
                if (received + chunk.value.byteLength > size) throw new Error("Resource size mismatch: " + url);
                destination.set(chunk.value, received);
                received += chunk.value.byteLength;
                progress(received, false, received * wireScale);
                while (persist && (storedChunks + 1) * chunkSize <= received)
                    await storeChunk((storedChunks + 1) * chunkSize);
            }
            if (received !== size) throw new Error("Resource size mismatch: " + url);
            if (persist && storedChunks < chunkCount) await storeChunk(size);
            if ((await digest(destination)) !== sha256) throw new Error("Resource SHA-256 mismatch: " + url);
            checkCancelled(signal);
            if (persist) {
                try {
                    await cache.put(
                        marker,
                        new ResponseType(JSON.stringify({ sha256, size, chunkSize, chunkCount }), {
                            headers: { "Content-Type": "application/json" },
                        }),
                    );
                } catch (error) {
                    cacheUnavailable();
                    await removeMarker(cache, marker);
                    if (options.requirePersistence) throw error;
                }
                checkCancelled(signal);
            }
            return { bytes: destination, cached: false };
        } catch (error) {
            if (cache) await removeMarker(cache, marker);
            throw error;
        } finally {
            if (!ended) {
                try {
                    await reader.cancel();
                } catch {}
            }
            reader.releaseLock();
        }
    }
    return { readOwned };
});
