(function (root, factory) {
    "use strict";
    if (typeof module === "object" && module && module.exports) module.exports = factory;
    else root.createFmodBankPageSource = factory;
})(typeof globalThis !== "undefined" ? globalThis : this, function (index, resolveUrl, cacheFactory, options) {
    "use strict";
    options = options || {};
    if (index.version !== 1 || index.pageSize !== 1024 * 1024) throw new Error("Unsupported bank page index.");
    const states = index.banks.map((bank) => {
        if (
            !Number.isSafeInteger(bank.size) ||
            bank.size < 0 ||
            bank.pages.length !== Math.ceil(bank.size / index.pageSize) ||
            !/^[a-f0-9]{64}$/i.test(bank.sha256) ||
            bank.pages.some((hash) => !/^[a-f0-9]{64}$/i.test(hash))
        ) {
            throw new Error("Invalid bank page index.");
        }
        return bank.pages.map(() => ({ bytes: null, pending: null, received: 0, cached: false, ready: false }));
    });
    const requests = new Map();
    const lifetime = new AbortController();
    const abort = () => lifetime.abort(options.signal.reason);
    if (options.signal) {
        if (options.signal.aborted) abort();
        else options.signal.addEventListener("abort", abort, { once: true });
    }
    const actualFetch = options.fetch || ((url, settings) => fetch(url, settings));
    const cache = cacheFactory({
        ...(options.cacheSettings || {}),
        fetch: async function (url, settings) {
            const range = requests.get(url);
            if (!range) throw new Error("Bank range request is no longer active.");
            const response = await actualFetch(range.url, {
                ...settings,
                headers: { Range: "bytes=" + range.start + "-" + range.end },
            });
            if (
                response.status !== 206 ||
                response.headers.get("Content-Range") !== "bytes " + range.start + "-" + range.end + "/" + range.total
            ) {
                if (response.body) await response.body.cancel();
                throw new Error("Server did not return the requested bank range.");
            }
            return response;
        },
    });
    function length(file, page) {
        return Math.min(index.pageSize, index.banks[file].size - page * index.pageSize);
    }
    function getPage(file, page) {
        const state = states[file] && states[file][page];
        if (!state) return Promise.reject(new Error("Bank page is outside the index."));
        if (lifetime.signal.aborted) return Promise.reject(new Error("Bank page source was disposed."));
        if (state.bytes) return Promise.resolve(state.bytes);
        if (state.pending) return state.pending;
        const bank = index.banks[file];
        const size = length(file, page);
        const url = resolveUrl(bank);
        // This is request routing only; VerifiedContentCache keys persistent data by SHA and size.
        const key = url + "#celeste-bank-page=" + page;
        requests.set(key, {
            url,
            start: page * index.pageSize,
            end: page * index.pageSize + size - 1,
            total: bank.size,
        });
        state.received = 0;
        state.ready = false;
        state.pending = cache
            .readOwned(key, size, bank.pages[page], {
                signal: lifetime.signal,
                requirePersistence: options.requirePersistence === true,
                progress: function (received, cached, downloaded) {
                    state.received = received;
                    state.cached = cached;
                    if (options.transferProgress) options.transferProgress(file, page, received, cached, downloaded);
                    if (options.progress) options.progress();
                },
            })
            .then((result) => {
                lifetime.signal.throwIfAborted();
                if (options.retainBytes !== false) state.bytes = result.bytes;
                state.received = size;
                state.cached = result.cached;
                state.ready = true;
                if (options.progress) options.progress();
                return result.bytes;
            })
            .finally(() => {
                state.pending = null;
                requests.delete(key);
            });
        return state.pending;
    }
    function progress(pages) {
        const result = {
            totalBytes: 0,
            receivedBytes: 0,
            readyBytes: 0,
            cachedBytes: 0,
            totalPages: pages.length,
            readyPages: 0,
        };
        for (const [file, page] of pages) {
            const state = states[file][page];
            const size = length(file, page);
            result.totalBytes += size;
            result.receivedBytes += state.received;
            if (state.ready) {
                result.readyBytes += size;
                result.readyPages++;
                if (state.cached) result.cachedBytes += size;
            }
        }
        return result;
    }
    async function prepare(pages) {
        for (const [file, page] of pages) await getPage(file, page);
    }
    function dispose() {
        if (options.signal) options.signal.removeEventListener("abort", abort);
        lifetime.abort();
    }
    return { getPage, prepare, progress, dispose };
});
