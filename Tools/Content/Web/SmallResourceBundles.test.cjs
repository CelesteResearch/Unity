"use strict";
const assert = require("node:assert/strict");
const crypto = require("node:crypto");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const hash = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex");
const cacheSource = fs.readFileSync(path.join(__dirname, "VerifiedContentCache.js"), "utf8");
const workerSource = fs.readFileSync(path.join(__dirname, "FullContentDownload.Worker.js"), "utf8");
const pageSource = fs.readFileSync(path.join(__dirname, "../../Audio/Fmod/Html5/FmodBankPageSource.js"), "utf8");

class MemoryCache {
    constructor() {
        this.values = new Map();
        this.quota = false;
    }
    async match(key) {
        return this.values.get(String(key))?.clone();
    }
    async delete(key) {
        return this.values.delete(String(key));
    }
    async put(key, response) {
        if (this.quota) throw new DOMException("Storage quota exceeded", "QuotaExceededError");
        this.values.set(String(key), response.clone());
    }
}

const payloads = Array.from({ length: 8 }, (_, index) => Buffer.from(("resource-" + index + " ").repeat(37)));
const files = payloads.map((bytes, index) => ({
    Path: "Graphics/test" + index + ".data",
    ResourcePath: "objects/" + hash(bytes) + "/test" + index + ".data",
    Size: bytes.length,
    Sha256: hash(bytes),
}));
const packed = Buffer.concat(payloads);
let offset = 0;
const bundle = {
    ResourcePath: "bundles/" + hash(packed) + ".data",
    Size: packed.length,
    Sha256: hash(packed),
    Files: files.map((file) => {
        const entry = { Path: file.Path, Offset: offset };
        offset += file.Size;
        return entry;
    }),
};
const manifest = { SchemaVersion: 1, Files: files, SmallResourceBundles: [bundle] };

async function run(options = {}) {
    const cache = options.cache || new MemoryCache();
    const requests = [];
    let activeBundles = 0,
        maximumActiveBundles = 0;
    let settle, timer, context;
    const complete = new Promise((resolve, reject) => {
        settle = resolve;
        timer = setTimeout(() => reject(new Error("Worker fixture timed out")), 5000);
    });
    const fetch = async (url, init) => {
        init?.signal?.throwIfAborted();
        const relative = String(url).replace("https://celeste.test/", "");
        if (relative === "manifest") return Response.json(options.manifest || manifest);
        if (relative === "banks")
            return Response.json(options.bankIndex || { version: 1, pageSize: 1048576, banks: [] });
        requests.push(relative);
        const bundleBytes =
            options.bundlePayloads?.get(relative) || (relative === "content/" + bundle.ResourcePath ? packed : null);
        if (bundleBytes) {
            activeBundles++;
            maximumActiveBundles = Math.max(maximumActiveBundles, activeBundles);
            await new Promise((resolve) => setTimeout(resolve, 10));
            activeBundles--;
            if (options.abort) {
                context.self.onmessage({ data: { type: "cancel" } });
                init.signal.throwIfAborted();
            }
            if (options.httpFailure) return new Response("missing", { status: 503 });
            const bytes = options.corrupt
                ? Buffer.from(bundleBytes).fill(7, 0, 10)
                : options.truncated
                  ? bundleBytes.subarray(0, bundleBytes.length - 1)
                  : bundleBytes;
            const headers = options.unknownLength ? {} : { "Content-Length": String(bundleBytes.length / 2) };
            return new Response(bytes, { headers });
        }
        if (options.bank && relative === "content/" + options.bank.ResourcePath) {
            assert.equal(init.headers.Range, "bytes=0-" + (options.bank.Size - 1));
            return new Response(options.bank.bytes, {
                status: 206,
                headers: {
                    "Content-Range": "bytes 0-" + (options.bank.Size - 1) + "/" + options.bank.Size,
                    "Content-Length": String(options.bank.Size),
                },
            });
        }
        const index = files.findIndex((file) => relative === "content/" + file.ResourcePath);
        if (index < 0) throw new Error("Unexpected request: " + relative);
        return new Response(payloads[index], { headers: { "Content-Length": String(payloads[index].length) } });
    };
    context = vm.createContext({
        crypto: crypto.webcrypto,
        caches: { open: async () => cache },
        fetch,
        Response,
        Uint8Array,
        URL,
        DOMException,
        AbortController,
        performance,
        setTimeout,
        clearTimeout,
        console,
        location: { origin: "https://celeste.test" },
        postMessage: (message) => {
            if (message.type === "settled") {
                clearTimeout(timer);
                settle(message.progress);
            }
        },
    });
    context.self = context;
    context.importScripts = (...names) =>
        names.forEach((name) => vm.runInContext(name === "cache.js" ? cacheSource : pageSource, context));
    vm.runInContext(workerSource, context);
    context.self.onmessage({
        data: {
            type: "start",
            settings: {
                manifestUrl: "https://celeste.test/manifest",
                bankIndexUrl: "https://celeste.test/banks",
                contentBaseUrl: "https://celeste.test/content/",
                cacheScriptUrl: "cache.js",
                pageSourceScriptUrl: "pages.js",
            },
        },
    });
    const progress = await complete;
    return { progress, requests, cache, maximumActiveBundles };
}

function completed(result) {
    assert.equal(result.progress.state, "Completed", result.progress.error);
    assert.equal(result.progress.readyFiles, files.length);
    assert.equal(result.progress.readyBytes, packed.length);
}

(async () => {
    const cold = await run();
    completed(cold);
    assert.equal(cold.requests.length, 1);
    assert.equal(cold.progress.downloadedBytes, packed.length / 2);
    const warm = await run({ cache: cold.cache });
    completed(warm);
    assert.equal(warm.requests.length, 0);
    assert.equal(warm.progress.cachedBytes, packed.length);
    assert.equal(warm.progress.downloadedBytes, 0);

    const partial = await run();
    for (const key of Array.from(partial.cache.values.keys()))
        if (key.includes(files[0].Sha256)) partial.cache.values.delete(key);
    const sparse = await run({ cache: partial.cache });
    completed(sparse);
    assert.deepEqual(sparse.requests, ["content/" + files[0].ResourcePath]);

    for (const failure of ["httpFailure", "corrupt", "truncated"]) {
        const result = await run({ [failure]: true });
        completed(result);
        assert.equal(result.requests.filter((value) => value.includes("bundles/")).length, 1);
        assert.equal(result.requests.filter((value) => value.includes("objects/")).length, files.length);
    }
    const malformed = structuredClone(manifest);
    malformed.SmallResourceBundles[0].Files[1].Offset++;
    const invalid = await run({ manifest: malformed });
    completed(invalid);
    assert.equal(invalid.requests.length, files.length);
    assert(!invalid.requests.some((value) => value.includes("bundles/")));

    const unknown = await run({ unknownLength: true });
    completed(unknown);
    assert.equal(unknown.progress.downloadedBytes, 0);

    const mismatched = structuredClone(manifest);
    [mismatched.SmallResourceBundles[0].Files[0].Path, mismatched.SmallResourceBundles[0].Files[1].Path] = [
        mismatched.SmallResourceBundles[0].Files[1].Path,
        mismatched.SmallResourceBundles[0].Files[0].Path,
    ];
    const memberHash = await run({ manifest: mismatched });
    completed(memberHash);
    assert.equal(memberHash.requests.filter((value) => value.includes("objects/")).length, 2);

    const multiple = structuredClone(manifest);
    multiple.SmallResourceBundles = [];
    const bundlePayloads = new Map();
    for (let start = 0; start < files.length; start += 2) {
        const bytes = Buffer.concat(payloads.slice(start, start + 2));
        const resource = "bundles/" + hash(bytes) + ".data";
        multiple.SmallResourceBundles.push({
            ResourcePath: resource,
            Size: bytes.length,
            Sha256: hash(bytes),
            Files: [
                { Path: files[start].Path, Offset: 0 },
                { Path: files[start + 1].Path, Offset: files[start].Size },
            ],
        });
        bundlePayloads.set("content/" + resource, bytes);
    }
    const bounded = await run({ manifest: multiple, bundlePayloads });
    completed(bounded);
    assert.equal(bounded.requests.length, 4);
    assert.equal(bounded.maximumActiveBundles, 2);

    const bankBytes = Buffer.from("unmodified FMOD bank range");
    const bank = {
        Path: "Audio/test.bank",
        ResourcePath: "objects/" + hash(bankBytes) + "/test.bank",
        Size: bankBytes.length,
        Sha256: hash(bankBytes),
        bytes: bankBytes,
    };
    const withBank = structuredClone(manifest);
    withBank.Files.push(bank);
    const bankResult = await run({
        manifest: withBank,
        bank,
        bankIndex: {
            version: 1,
            pageSize: 1048576,
            banks: [{ path: bank.Path, size: bank.Size, sha256: bank.Sha256, pages: [bank.Sha256] }],
        },
    });
    assert.equal(bankResult.progress.state, "Completed", bankResult.progress.error);
    assert.equal(bankResult.progress.readyBytes, packed.length + bank.Size);
    assert.equal(bankResult.progress.readyFiles, files.length + 1);
    assert.equal(bankResult.requests.filter((value) => value.endsWith(".bank")).length, 1);

    const cancelled = await run({ abort: true });
    assert.equal(cancelled.progress.state, "Cancelled");
    assert.equal(cancelled.requests.length, 1);

    const limited = new MemoryCache();
    limited.quota = true;
    const quota = await run({ cache: limited });
    assert.equal(quota.progress.state, "Failed");
    assert.match(quota.progress.error, /quota/i);
    assert(!quota.requests.some((value) => value.includes("objects/")));

    console.log(
        JSON.stringify({
            status: "pass",
            scenarios: 14,
            maximumActiveBundles: bounded.maximumActiveBundles,
            bankRangePreserved: true,
            coldResourceRequests: cold.requests.length,
            warmResourceRequests: warm.requests.length,
            encodedBytesCountedOnce: cold.progress.downloadedBytes,
        }),
    );
})().catch((error) => {
    console.error(error);
    process.exitCode = 1;
});
