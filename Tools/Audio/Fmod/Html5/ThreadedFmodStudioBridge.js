(function (root, factory) {
    if (typeof module === "object" && module && module.exports) module.exports = factory;
    else root.createThreadedFmodStudioBridge = factory;
})(
    typeof globalThis !== "undefined" ? globalThis : this,
    function (heap, handlesFactory, bankServiceFactory, bankIndex) {
        "use strict";
        const modules = new Map();
        const requests = new Map();
        const fileRequests = new Map();
        const scriptLoads = new Map();
        let nextModule = 1;
        const vectorNames = ["position", "velocity", "forward", "up"];
        const axisNames = ["x", "y", "z"];
        function state(id) {
            const value = modules.get(id);
            if (!value) throw new Error("FMOD module has been disposed.");
            return value;
        }
        function reset(out) {
            out.val = undefined;
            return out;
        }
        function writeInt(ptr, value) {
            heap.i32()[ptr >>> 2] = value | 0;
        }
        function writeFloat(ptr, value) {
            heap.f32()[ptr >>> 2] = value;
        }
        function call(id, token, kind, action) {
            const s = modules.get(id);
            if (!s) return 30;
            const object = s.handles.get(token, kind);
            if (!object) return s.fmod.ERR_INVALID_HANDLE;
            return action(s, object);
        }
        function scalar(id, token, kind, method, ptr, floating) {
            const s = modules.get(id);
            if (!s) return 30;
            const object = s.handles.get(token, kind);
            if (!object) return s.fmod.ERR_INVALID_HANDLE;
            const result = object[method](reset(s.out));
            if (result === s.fmod.OK) (floating ? writeFloat : writeInt)(ptr, s.out.val);
            return result;
        }
        function pair(id, token, kind, method, first, second) {
            const s = modules.get(id);
            if (!s) return 30;
            const object = s.handles.get(token, kind);
            if (!object) return s.fmod.ERR_INVALID_HANDLE;
            const result = object[method](reset(s.out), reset(s.second));
            if (result === s.fmod.OK) {
                writeFloat(first, s.out.val);
                writeFloat(second, s.second.val);
            }
            return result;
        }
        function invoke(id, token, kind, method, value) {
            const s = modules.get(id);
            if (!s) return 30;
            const object = s.handles.get(token, kind);
            if (!object) return s.fmod.ERR_INVALID_HANDLE;
            return value === undefined ? object[method]() : object[method](value);
        }
        function objectResult(id, token, kind, method, argument, output, resultKind) {
            return call(id, token, kind, (s, object) => {
                const result =
                    argument === undefined ? object[method](reset(s.out)) : object[method](argument, reset(s.out));
                if (result === s.fmod.OK)
                    writeInt(output, s.handles.put(s.out.val, resultKind, s.handles.owner(token)));
                return result;
            });
        }
        function path(id, token, kind, output, capacity, retrieved) {
            return call(id, token, kind, (s, object) => {
                let result;
                do {
                    result = object.getPath(reset(s.out), s.pathCapacity, reset(s.second));
                    if (result === s.fmod.ERR_TRUNCATED) s.pathCapacity *= 2;
                } while (result === s.fmod.ERR_TRUNCATED);
                if (result !== s.fmod.OK) return result;
                const required = s.second.val;
                writeInt(retrieved, required);
                if (capacity > 0 && output) heap.writeUtf8(s.out.val, output, capacity);
                return required > capacity ? s.fmod.ERR_TRUNCATED : s.fmod.OK;
            });
        }
        function list(id, token, kind, method, output, capacity, count, resultKind) {
            return call(id, token, kind, (s, object) => {
                const result = object[method](reset(s.out), capacity, reset(s.second));
                if (result === s.fmod.OK || result === s.fmod.ERR_TRUNCATED) {
                    writeInt(count, s.second.val);
                    const values = s.out.val || [];
                    for (let i = 0; i < values.length && i < capacity; ++i)
                        writeInt(output + i * 4, s.handles.put(values[i], resultKind, s.handles.owner(token)));
                }
                return result;
            });
        }
        function writeGuid(ptr, guid) {
            const bytes = heap.u8();
            const words = heap.u32();
            words[ptr >>> 2] = guid.Data1;
            bytes[ptr + 4] = guid.Data2 & 255;
            bytes[ptr + 5] = guid.Data2 >>> 8;
            bytes[ptr + 6] = guid.Data3 & 255;
            bytes[ptr + 7] = guid.Data3 >>> 8;
            for (let i = 0; i < 8; ++i) bytes[ptr + 8 + i] = guid.Data4[i];
        }
        function readGuid(s, ptr) {
            const bytes = heap.u8();
            const guid = s.guid;
            guid.Data1 = heap.u32()[ptr >>> 2];
            guid.Data2 = bytes[ptr + 4] | (bytes[ptr + 5] << 8);
            guid.Data3 = bytes[ptr + 6] | (bytes[ptr + 7] << 8);
            for (let i = 0; i < 8; ++i) guid.Data4[i] = bytes[ptr + 8 + i];
            return guid;
        }
        function guid(id, token, kind, ptr) {
            return call(id, token, kind, (s, object) => {
                const result = object.getID(reset(s.out));
                if (result === s.fmod.OK) writeGuid(ptr, s.out.val);
                return result;
            });
        }
        function parameter(id, token, byName, argument, output) {
            return call(id, token, "description", (s, object) => {
                const value = s.parameter;
                value.name = undefined;
                const result = byName
                    ? object.getParameterDescriptionByName(argument, value)
                    : object.getParameterDescriptionByIndex(argument, value);
                if (result !== s.fmod.OK) return result;
                if (value.type > 6) return s.fmod.ERR_UNSUPPORTED;
                let index = argument;
                if (byName) {
                    let indices = s.parameterIndices.get(object);
                    if (!indices) {
                        const countResult = object.getParameterDescriptionCount(reset(s.second));
                        if (countResult !== s.fmod.OK) return countResult;
                        indices = new Map();
                        const count = s.second.val;
                        const item = {};
                        for (let i = 0; i < count; i++) {
                            const itemResult = object.getParameterDescriptionByIndex(i, item);
                            if (itemResult !== s.fmod.OK) return itemResult;
                            indices.set(item.id.data1 + ":" + item.id.data2, i);
                        }
                        s.parameterIndices.set(object, indices);
                    }
                    index = indices.get(value.id.data1 + ":" + value.id.data2);
                    if (index === undefined) return s.fmod.ERR_EVENT_NOTFOUND;
                }
                const name = value.name == null ? 0 : heap.allocateUtf8(value.name);
                writeInt(output, name);
                writeInt(output + 4, index);
                writeFloat(output + 8, value.minimum);
                writeFloat(output + 12, value.maximum);
                writeFloat(output + 16, value.defaultvalue);
                writeInt(output + 20, value.type);
                return s.fmod.OK;
            });
        }
        function attrs(s, ptr) {
            const floats = heap.f32();
            for (let i = 0; i < 4; ++i) {
                const vector = s.attributes[vectorNames[i]];
                vector.x = floats[(ptr >>> 2) + i * 3];
                vector.y = floats[(ptr >>> 2) + i * 3 + 1];
                vector.z = floats[(ptr >>> 2) + i * 3 + 2];
            }
            return s.attributes;
        }
        function loadScript(url) {
            if (scriptLoads.has(url)) return scriptLoads.get(url);
            const promise = new Promise((resolve, reject) => {
                const script = document.createElement("script");
                script.src = url;
                script.onload = () =>
                    typeof FMODModule === "function"
                        ? resolve(FMODModule)
                        : reject(new Error("FMODModule export missing."));
                script.onerror = () => {
                    scriptLoads.delete(url);
                    script.remove();
                    reject(new Error("FMOD SDK script could not be loaded: " + url));
                };
                document.head.appendChild(script);
            });
            scriptLoads.set(url, promise);
            return promise;
        }
        function installAudioGesture(s) {
            if (s.gesture) return;
            s.gesture = function () {
                if (s.context.state === "suspended")
                    s.context.resume().catch((error) => console.error("FMOD audio resume: " + error));
            };
            document.addEventListener("click", s.gesture);
            document.addEventListener("keydown", s.gesture);
            document.addEventListener("touchend", s.gesture);
        }
        function removeAudioGesture(s) {
            if (!s.gesture) return;
            document.removeEventListener("click", s.gesture);
            document.removeEventListener("keydown", s.gesture);
            document.removeEventListener("touchend", s.gesture);
            s.gesture = null;
        }
        function freeAudioOutput(s, owner, output) {
            if (output.freed) return;
            const result = s.fmod._ReleaseOutput(owner);
            if (result !== s.fmod.OK) throw new Error("FMOD output release: " + s.fmod.ErrorString(result));
            output.freed = true;
            if (output.node) output.node.port.close();
            s.audioOutputs.delete(owner);
        }
        function closeAudioOutput(s, owner) {
            const output = s.audioOutputs.get(owner);
            if (!output || output.closed) return;
            output.closed = true;
            if (!output.node) {
                freeAudioOutput(s, owner, output);
                return;
            }
            output.node.disconnect();
            output.node.port.onmessage = (event) => {
                if (event.data === "closed") freeAudioOutput(s, owner, output);
            };
            output.node.port.postMessage("close");
        }
        function closeAudioContext(s) {
            if (s.closingAudio) return s.closingAudio;
            removeAudioGesture(s);
            s.closingAudio = s.context.close().then(async () => {
                for (const [owner, output] of s.audioOutputs) freeAudioOutput(s, owner, output);
                await s.bankService.close();
                s.fmod.PThread.terminateAllThreads();
            });
            return s.closingAudio;
        }
        function releaseState(s) {
            if (s.fileProgressTimer) {
                clearTimeout(s.fileProgressTimer);
                s.fileProgressTimer = null;
            }
            removeAudioGesture(s);
            for (const [requestId, request] of fileRequests) {
                if (request.module !== s.id) continue;
                request.cancelled = true;
                s.bankService.cancel(requestId);
                fileRequests.delete(requestId);
            }
            let failure;
            try {
                s.handles.dispose((studio) => studio.release());
            } catch (error) {
                failure = error;
            }
            for (const studioId of s.audioOutputs.keys()) {
                try {
                    closeAudioOutput(s, studioId);
                } catch (error) {
                    failure = failure || error;
                }
            }
            s.files.clear();
            modules.delete(s.id);
            closeAudioContext(s).catch((error) => console.error("FMOD audio disposal: " + error));
            if (failure) throw failure;
        }
        function reportFileProgress(s, force) {
            const now = performance.now();
            const elapsed = s.lastFileProgressAt === undefined ? Infinity : now - s.lastFileProgressAt;
            if (elapsed < 250) {
                if (force && !s.fileProgressTimer)
                    s.fileProgressTimer = setTimeout(
                        () => {
                            s.fileProgressTimer = null;
                            if (modules.has(s.id)) reportFileProgress(s, true);
                        },
                        Math.ceil(250 - elapsed),
                    );
                return;
            }
            if (s.fileProgressTimer) {
                clearTimeout(s.fileProgressTimer);
                s.fileProgressTimer = null;
            }
            s.lastFileProgressAt = now;
            window.dispatchEvent(
                new CustomEvent("celeste-content-progress", {
                    detail: {
                        category: "audio",
                        ...fileProgress(s),
                    },
                }),
            );
        }
        function fileProgress(s) {
            return s.bankProgress;
        }
        const api = {
            getFileProgress(id) {
                return fileProgress(state(id));
            },
            ReadBankFileProgress(id, totalBytes, receivedBytes, readyBytes, totalFiles, readyFiles, cachedBytes) {
                const progress = fileProgress(state(id));
                const doubles = heap.f64();
                doubles[totalBytes >>> 3] = progress.totalBytes;
                doubles[receivedBytes >>> 3] = progress.receivedBytes;
                doubles[readyBytes >>> 3] = progress.readyBytes;
                doubles[cachedBytes >>> 3] = progress.cachedBytes;
                writeInt(totalFiles, progress.totalFiles);
                writeInt(readyFiles, progress.readyFiles);
            },
            async load(requestId, scriptUrl, wasmUrl, complete) {
                if (requests.has(requestId)) throw new Error("Duplicate FMOD request.");
                const request = { controller: new AbortController(), cancelled: false };
                requests.set(requestId, request);
                let fmod, context, bankService;
                let bankProgress = {
                    totalBytes: 0,
                    receivedBytes: 0,
                    readyBytes: 0,
                    totalFiles: 0,
                    readyFiles: 0,
                    cachedBytes: 0,
                };
                let moduleState;
                let transferred = false;
                try {
                    if (!globalThis.crossOriginIsolated)
                        throw new Error("FMOD threaded audio requires cross-origin isolation.");
                    const factory = await loadScript(scriptUrl);
                    const response = await fetch(wasmUrl, { signal: request.controller.signal });
                    if (!response.ok) throw new Error("FMOD WASM HTTP " + response.status);
                    const binary = await response.arrayBuffer();
                    if (request.cancelled) return;
                    fmod = await factory({
                        wasmBinary: binary,
                        locateFile: (name) => new URL(name, scriptUrl).href,
                        mainScriptUrlOrBlob: scriptUrl,
                        print: (text) => console.log(text),
                        printErr: (text) => console.error(text),
                    });
                    if (request.cancelled) return;
                    bankService = await bankServiceFactory(fmod, scriptUrl, bankIndex, (progress) => {
                        bankProgress = {
                            totalBytes: progress.totalBytes,
                            receivedBytes: progress.receivedBytes,
                            readyBytes: progress.readyBytes,
                            totalFiles: progress.totalFiles,
                            readyFiles: progress.readyFiles,
                            cachedBytes: progress.cachedBytes,
                        };
                        if (moduleState && modules.has(moduleState.id)) {
                            moduleState.bankProgress = bankProgress;
                            reportFileProgress(moduleState, false);
                        }
                    });
                    if (request.cancelled) return;
                    context = new AudioContext();
                    await context.audioWorklet.addModule(new URL("ThreadedFmodOutput.Worklet.js", scriptUrl).href);
                    if (request.cancelled) return;
                    const output = {};
                    let studio;
                    let core;
                    try {
                        let result = fmod.Studio_System_Create(output);
                        if (result !== fmod.OK) throw new Error("FMOD create: " + fmod.ErrorString(result));
                        studio = output.val;
                        result = studio.getCoreSystem(output);
                        core = output.val;
                        if (result !== fmod.OK) throw new Error("FMOD core: " + fmod.ErrorString(result));
                        result = core.getVersion(output, null);
                        if (result !== fmod.OK || output.val !== 0x00020315)
                            throw new Error("Expected FMOD 2.03.15 runtime.");
                    } finally {
                        if (studio) studio.release();
                        if (core) core.delete();
                        if (studio) studio.delete();
                    }
                    if (nextModule > 0x7fffffff) throw new Error("FMOD module identities are exhausted.");
                    const id = nextModule++;
                    const s = {
                        id,
                        fmod,
                        files: new Map(),
                        audioOutputs: new Map(),
                        context,
                        parameterIndices: new WeakMap(),
                        outputTypes: new Map(),
                        handles: handlesFactory(fmod),
                        out: {},
                        second: {},
                        pathCapacity: 256,
                        gesture: null,
                        attributes: { position: {}, velocity: {}, forward: {}, up: {} },
                        parameter: {},
                        guid: { Data1: 0, Data2: 0, Data3: 0, Data4: new Uint8Array(8) },
                    };
                    s.bankService = bankService;
                    s.bankProgress = bankProgress;
                    moduleState = s;
                    modules.set(id, s);
                    transferred = true;
                    installAudioGesture(s);
                    requests.delete(requestId);
                    request.completed = true;
                    complete(requestId, id, 0);
                } catch (error) {
                    if (!request.cancelled && !request.completed) {
                        requests.delete(requestId);
                        complete(requestId, 0, heap.allocateUtf8(String((error && error.message) || error)));
                    }
                } finally {
                    requests.delete(requestId);
                    if (!transferred) {
                        if (context) await context.close();
                        if (bankService) await bankService.close();
                        if (fmod) fmod.PThread.terminateAllThreads();
                    }
                }
            },
            cancelLoad(requestId) {
                const request = requests.get(requestId);
                if (!request) return;
                request.cancelled = true;
                request.controller.abort();
                requests.delete(requestId);
            },
            disposeModule(id) {
                const s = modules.get(id);
                if (s) releaseState(s);
            },
            free(id, ptr) {
                if (ptr) heap.free(ptr);
            },
            async prepareBankFile(requestId, id, filename, url, size, sha256, complete) {
                const request = { module: id, cancelled: false, completed: false };
                try {
                    const s = state(id);
                    if (fileRequests.has(requestId)) throw new Error("Duplicate FMOD file request.");
                    fileRequests.set(requestId, request);
                    if (
                        !filename.startsWith("/") ||
                        filename.includes("\\") ||
                        filename
                            .split("/")
                            .slice(1)
                            .some((part) => !part || part === "." || part === "..")
                    )
                        throw new Error("A canonical absolute FMOD bank path is required.");
                    if (!Number.isSafeInteger(size) || size < 0 || !/^[a-f0-9]{64}$/i.test(sha256))
                        throw new Error("Invalid FMOD bank manifest metadata.");
                    const existing = s.files.get(filename);
                    if (existing) {
                        if (existing.url !== url || existing.size !== size || existing.sha256 !== sha256.toLowerCase())
                            throw new Error("The FMOD bank path already has different content.");
                    } else {
                        const fileId = bankIndex.banks.findIndex((bank) => filename.endsWith("/" + bank.path));
                        const bank = bankIndex.banks[fileId];
                        if (!bank || bank.size !== size || bank.sha256.toLowerCase() !== sha256.toLowerCase())
                            throw new Error("The FMOD bank does not match the built page index.");
                        request.filename = filename;
                        reportFileProgress(s, true);
                        await s.bankService.prepare(requestId, fileId, url, size, sha256.toLowerCase());
                        if (request.cancelled || !modules.has(id)) return;
                        s.files.set(filename, { url, size, sha256: sha256.toLowerCase(), fileId });
                    }
                    fileRequests.delete(requestId);
                    request.completed = true;
                    reportFileProgress(s, true);
                    complete(requestId, 0);
                } catch (error) {
                    if (!request.cancelled && !request.completed) {
                        fileRequests.delete(requestId);
                        complete(requestId, heap.allocateUtf8(String((error && error.message) || error)));
                    }
                } finally {
                    fileRequests.delete(requestId);
                }
            },
            cancelPrepareBankFile(requestId) {
                const request = fileRequests.get(requestId);
                if (!request) return;
                request.cancelled = true;
                const s = modules.get(request.module);
                if (s) s.bankService.cancel(requestId);
                fileRequests.delete(requestId);
            },
            StudioSystemCreate(id, output, headerVersion) {
                const s = state(id);
                if (headerVersion !== 0x00020315) return s.fmod.ERR_HEADER_MISMATCH;
                const result = s.fmod.Studio_System_Create(reset(s.out));
                if (result === s.fmod.OK) writeInt(output, s.handles.put(s.out.val, "studio", 0));
                return result;
            },
            StudioSystemInitialize(id, studio, channels, studioFlags, flags, extra) {
                if (extra) return 31;
                return call(id, studio, "studio", (s, object) => {
                    if (flags & 0x00400000) return s.fmod.ERR_UNSUPPORTED;
                    const previousOutput = s.audioOutputs.get(studio);
                    if (previousOutput && previousOutput.node) return s.fmod.ERR_INITIALIZED;
                    let result = object.getCoreSystem(reset(s.out));
                    if (result !== s.fmod.OK) return result;
                    const core = s.out.val;
                    try {
                        const outputType = s.outputTypes.get(studio);
                        if (outputType === 2 || outputType === 4) {
                            result = s.fmod._CelesteBankIO_PrepareOwner(studio);
                            return result === s.fmod.OK
                                ? object.initialize(channels, studioFlags, flags, null)
                                : result;
                        }
                        result = s.fmod._PrepareOutput(studio, s.context.sampleRate);
                        if (result !== s.fmod.OK) return result;
                        const output = previousOutput || { node: null, closed: false, freed: false };
                        s.audioOutputs.set(studio, output);
                        result = s.fmod._CelesteBankIO_PrepareOwner(studio);
                        if (result !== s.fmod.OK) return result;
                        result = object.initialize(channels, studioFlags, flags, null);
                        if (result !== s.fmod.OK) {
                            if (!object.isValid() && s.fmod._ReleaseOutput(studio) === s.fmod.OK) {
                                output.freed = true;
                                s.audioOutputs.delete(studio);
                            }
                            return result;
                        }
                        const pointer = s.fmod._GetOutputRing(studio);
                        if (!pointer) throw new Error("FMOD initialized without its owned output ring.");
                        output.node = new AudioWorkletNode(s.context, "celeste-fmod-output", {
                            numberOfInputs: 0,
                            numberOfOutputs: 1,
                            outputChannelCount: [2],
                            processorOptions: { memory: s.fmod.wasmMemory.buffer, pointer },
                        });
                        output.node.connect(s.context.destination);
                        return result;
                    } finally {
                        core.delete();
                    }
                });
            },
            StudioSystemLoadBankMemory(id, studio, buffer, length, output) {
                return call(id, studio, "studio", (s, object) => {
                    const result = object.loadBankMemory(
                        heap.u8().subarray(buffer, buffer + length),
                        length,
                        s.fmod.STUDIO_LOAD_MEMORY,
                        s.fmod.STUDIO_LOAD_BANK_NORMAL,
                        reset(s.out),
                    );
                    if (result === s.fmod.OK) writeInt(output, s.handles.put(s.out.val, "bank", studio));
                    return result;
                });
            },
            StudioSystemLoadBankFile(id, studio, filename, flags, output) {
                return call(id, studio, "studio", (s, object) => {
                    const path = heap.readUtf8(filename);
                    const file = s.files.get(path);
                    if (!file) return s.fmod.ERR_FILE_NOTFOUND;
                    const result = object.loadBankFile(
                        "/celeste-audio/" + studio + "/" + file.fileId,
                        flags,
                        reset(s.out),
                    );
                    if (result === s.fmod.OK) writeInt(output, s.handles.put(s.out.val, "bank", studio));
                    return result;
                });
            },
            StudioSystemRelease(id, studio) {
                return call(id, studio, "studio", (s, object) => {
                    const result = object.release();
                    if (result === s.fmod.OK) {
                        try {
                            s.handles.releaseSystem(studio);
                        } finally {
                            closeAudioOutput(s, studio);
                            s.outputTypes.delete(studio);
                            s.parameterIndices = new WeakMap();
                        }
                    }
                    return result;
                });
            },
            StudioSystemSetListenerAttributes(id, studio, listener, attributes) {
                const s = modules.get(id);
                if (!s) return 30;
                const object = s.handles.get(studio, "studio");
                if (!object) return s.fmod.ERR_INVALID_HANDLE;
                return object.setListenerAttributes(listener, attrs(s, attributes), null);
            },
            StudioEventInstanceSet3DAttributes(id, event, attributes) {
                const s = modules.get(id);
                if (!s) return 30;
                const object = s.handles.get(event, "instance");
                if (!object) return s.fmod.ERR_INVALID_HANDLE;
                return object.set3DAttributes(attrs(s, attributes));
            },
            StudioEventInstanceGet3DAttributes(id, event, output) {
                const s = modules.get(id);
                if (!s) return 30;
                const object = s.handles.get(event, "instance");
                if (!object) return s.fmod.ERR_INVALID_HANDLE;
                const result = object.get3DAttributes(s.attributes);
                if (result === s.fmod.OK)
                    for (let i = 0; i < 12; ++i)
                        writeFloat(output + i * 4, s.attributes[vectorNames[(i / 3) | 0]][axisNames[i % 3]]);
                return result;
            },
            StudioEventInstanceGetParameterValue(id, event, name, value, finalValue) {
                const s = modules.get(id);
                if (!s) return 30;
                const object = s.handles.get(event, "instance");
                if (!object) return s.fmod.ERR_INVALID_HANDLE;
                const result = object.getParameterByName(heap.readUtf8(name), reset(s.out), reset(s.second));
                if (result === s.fmod.OK) {
                    writeFloat(value, s.out.val);
                    writeFloat(finalValue, s.second.val);
                }
                return result;
            },
            StudioEventInstanceSetParameterValue(id, event, name, value) {
                const s = modules.get(id);
                if (!s) return 30;
                const object = s.handles.get(event, "instance");
                if (!object) return s.fmod.ERR_INVALID_HANDLE;
                return object.setParameterByName(heap.readUtf8(name), value, false);
            },
            StudioEventDescriptionGetParameter(id, description, name, output) {
                return parameter(id, description, true, heap.readUtf8(name), output);
            },
            StudioEventDescriptionGetParameterByIndex(id, description, index, output) {
                return parameter(id, description, false, index, output);
            },
            StudioEventDescriptionGetParameterInfoByIndex(id, description, index, output) {
                return parameter(id, description, false, index, output);
            },
            StudioSystemGetEventByID(id, studio, guidPtr, output) {
                return call(id, studio, "studio", (s, object) => {
                    const result = object.getEventByID(readGuid(s, guidPtr), reset(s.out));
                    if (result === s.fmod.OK) writeInt(output, s.handles.put(s.out.val, "description", studio));
                    return result;
                });
            },
            StudioEventInstanceRelease(id, event) {
                const result = invoke(id, event, "instance", "release");
                if (result === 0) state(id).handles.markReleased(event);
                return result;
            },
            StudioBankUnload(id, bank) {
                const result = invoke(id, bank, "bank", "unload");
                if (result === 0) {
                    state(id).handles.release(bank);
                    state(id).parameterIndices = new WeakMap();
                }
                return result;
            },
        };
        const families = [
            [
                "StudioBank",
                "bank",
                ["BusCount", "EventCount", "VCACount"],
                ["LoadSampleData"],
                ["EventList:description", "BusList:bus", "VCAList:vca"],
            ],
            ["StudioBus", "bus", ["Mute", "Paused"], [], []],
            [
                "StudioEventDescription",
                "description",
                ["InstanceCount", "Length", "Is3D", "IsOneshot"],
                ["LoadSampleData", "UnloadSampleData"],
                [],
            ],
            [
                "StudioEventInstance",
                "instance",
                ["Paused", "PlaybackState", "TimelinePosition"],
                ["Start", "KeyOff"],
                [],
            ],
            [
                "StudioSystem",
                "studio",
                ["BankCount"],
                ["FlushCommands", "FlushSampleLoading", "UnloadAll", "Update"],
                ["BankList:bank"],
            ],
            ["System", "core", ["Output", "Version"], [], []],
        ];
        for (const [prefix, kind, scalars, actions, lists] of families) {
            for (const name of scalars) {
                const method = name.startsWith("Is") ? name[0].toLowerCase() + name.slice(1) : "get" + name;
                const entry = prefix + (name.startsWith("Is") ? name : "Get" + name);
                api[entry] = (id, token, output) => scalar(id, token, kind, method, output, false);
            }
            for (const name of actions)
                api[prefix + name] = (id, token) => invoke(id, token, kind, name[0].toLowerCase() + name.slice(1));
            for (const spec of lists) {
                const [name, resultKind] = spec.split(":");
                api[prefix + "Get" + name] = (id, token, output, capacity, count) =>
                    list(id, token, kind, "get" + name, output, capacity, count, resultKind);
            }
            if (kind !== "studio" && kind !== "core" && kind !== "instance")
                api[prefix + "GetPath"] = (id, token, output, capacity, count) =>
                    path(id, token, kind, output, capacity, count);
            if (kind === "bank" || kind === "description")
                api[prefix + "GetID"] = (id, token, output) => guid(id, token, kind, output);
        }
        for (const [prefix, kind, name] of [
            ["StudioBus", "bus", "Volume"],
            ["StudioVCA", "vca", "Volume"],
            ["StudioEventInstance", "instance", "Volume"],
            ["StudioEventInstance", "instance", "Pitch"],
        ]) {
            api[prefix + "Get" + name] = (id, token, first, second) =>
                pair(id, token, kind, "get" + name, first, second);
            api[prefix + "Set" + name] = (id, token, value) => invoke(id, token, kind, "set" + name, value);
        }
        for (const [prefix, kind, names] of [
            ["StudioBus", "bus", ["Mute", "Paused", "StopAllEvents"]],
            ["StudioEventInstance", "instance", ["Paused", "TimelinePosition", "Stop"]],
            ["System", "core", ["Output"]],
        ])
            for (const name of names)
                api[prefix + (name === "Stop" || name === "StopAllEvents" ? name : "Set" + name)] = (
                    id,
                    token,
                    value,
                ) =>
                    invoke(
                        id,
                        token,
                        kind,
                        (name === "Stop" || name === "StopAllEvents" ? "" : "set") +
                            (name === "Stop" || name === "StopAllEvents"
                                ? name[0].toLowerCase() + name.slice(1)
                                : name),
                        name === "Mute" || name === "Paused" ? !!value : value,
                    );
        api.StudioSystemUpdate = (id, token) => {
            const result = invoke(id, token, "studio", "update");
            if (result === 0) state(id).handles.sweepReleased(token);
            return result;
        };
        api.StudioVCAGetPath = (id, token, output, capacity, count) => path(id, token, "vca", output, capacity, count);
        api.StudioSystemGetLowLevelSystem = (id, token, output) =>
            objectResult(id, token, "studio", "getCoreSystem", undefined, output, "core");
        for (const [name, kind] of [
            ["Event", "description"],
            ["Bus", "bus"],
            ["VCA", "vca"],
        ])
            api["StudioSystemGet" + name] = (id, token, value, output) =>
                objectResult(id, token, "studio", "get" + name, heap.readUtf8(value), output, kind);
        api.StudioEventDescriptionCreateInstance = (id, token, output) =>
            objectResult(id, token, "description", "createInstance", undefined, output, "instance");
        api.StudioEventInstanceGetDescription = (id, token, output) =>
            objectResult(id, token, "instance", "getDescription", undefined, output, "description");
        api.StudioEventInstanceTriggerCue = (id, token) => invoke(id, token, "instance", "keyOff");
        delete api.StudioEventInstanceKeyOff;
        api.StudioEventDescriptionGetParameterCount = (id, token, output) =>
            scalar(id, token, "description", "getParameterDescriptionCount", output, false);
        api.SystemGetVersion = (id, token, output) =>
            call(id, token, "core", (s, object) => {
                const result = object.getVersion(reset(s.out), null);
                if (result === s.fmod.OK) writeInt(output, s.out.val);
                return result;
            });
        api.StudioSystemUnloadAll = (id, token) => {
            const result = invoke(id, token, "studio", "unloadAll");
            if (result === 0) state(id).parameterIndices = new WeakMap();
            return result;
        };
        const legacyOutputNames = [
            "AUTODETECT",
            "UNKNOWN",
            "NOSOUND",
            "WAVWRITER",
            "NOSOUND_NRT",
            "WAVWRITER_NRT",
            "DSOUND",
            "WINMM",
            "WASAPI",
            "ASIO",
            "PULSEAUDIO",
            "ALSA",
            "COREAUDIO",
            "XAUDIO",
            "PS3",
            "AUDIOTRACK",
            "OPENSL",
            "WIIU",
            "AUDIOOUT",
            "AUDIO3D",
            "ATMOS",
            "WEBAUDIO",
            "NNAUDIO",
            "WINSONIC",
        ];
        api.SystemSetOutput = (id, token, value) =>
            call(id, token, "core", (s, object) => {
                const name = legacyOutputNames[value];
                if (!name) return s.fmod.ERR_INVALID_PARAM;
                const actual = s.fmod["OUTPUTTYPE_" + name];
                if (actual === undefined) return s.fmod.ERR_UNSUPPORTED;
                const result = object.setOutput(actual);
                if (result === s.fmod.OK) s.outputTypes.set(s.handles.owner(token), value);
                return result;
            });
        api.SystemGetOutput = (id, token, output) =>
            call(id, token, "core", (s, object) => {
                const result = object.getOutput(reset(s.out));
                if (result !== s.fmod.OK) return result;
                let value = legacyOutputNames.findIndex((name) => s.fmod["OUTPUTTYPE_" + name] === s.out.val);
                if (value < 0) value = s.out.val === s.fmod.OUTPUTTYPE_AUDIOWORKLET ? 21 : 1;
                writeInt(output, value);
                return result;
            });
        return api;
    },
);
