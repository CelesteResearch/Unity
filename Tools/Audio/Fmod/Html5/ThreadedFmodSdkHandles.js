(function (root, factory) {
    "use strict";
    const api = { create: factory };
    if (typeof module === "object" && module && module.exports) module.exports = api;
    else root.ThreadedFmodSdkHandles = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function (fmod) {
    "use strict";

    // The pinned FMOD 2.03.15 binding objects contain the native handle in their first
    // wasm32 word. $$.ptr itself identifies the independently allocated C++
    // wrapper, so repeated API borrows must not use it as a source handle.
    const types = {
        studio: "studioSystem*",
        core: "System*",
        bank: "Bank*",
        description: "EventDescription*",
        instance: "EventInstance*",
        bus: "Bus*",
        vca: "VCA*",
    };
    const entries = new Map();
    const systems = new Map();
    const nativeSystems = new Map();
    let nextId = 1;
    let disposed = false;

    function deleteWrapper(object) {
        if (object && object.$$ && object.$$.ptr) object.delete();
    }

    function nativeHandle(object, kind) {
        const pointer = object && object.$$;
        if (!types[kind] || !pointer || pointer.ptrType.name !== types[kind])
            throw new TypeError("The SDK object does not match its FMOD handle family.");
        const address = pointer.ptr;
        if (!Number.isInteger(address) || address <= 0 || (address & 3) !== 0 || address + 4 > fmod.HEAPU32.byteLength)
            throw new TypeError("The SDK wrapper address is unavailable in the current FMOD heap.");
        return fmod.HEAPU32[address >>> 2];
    }

    function put(object, kind, owner) {
        if (disposed) throw new Error("The FMOD handle owner has been disposed.");
        if (object == null) return 0;
        const native = nativeHandle(object, kind);
        if (native === 0) {
            deleteWrapper(object);
            return 0;
        }

        if (kind === "studio" && owner === 0) {
            const existing = nativeSystems.get(native);
            if (existing !== undefined) {
                if (entries.get(existing).object !== object) deleteWrapper(object);
                return existing;
            }
            // Studio creation produces a new native owner, not a borrowed one.
            owner = nextId;
        } else if (!entries.get(owner) || entries.get(owner).kind !== "studio") {
            deleteWrapper(object);
            throw new TypeError("A borrowed FMOD object requires its live Studio owner.");
        }

        let system = systems.get(owner);
        if (!system) {
            system = { ids: new Set(), families: new Map(), released: new Set() };
            systems.set(owner, system);
        }
        let family = system.families.get(kind);
        if (!family) {
            family = new Map();
            system.families.set(kind, family);
        }
        const existing = family.get(native);
        if (existing !== undefined) {
            if (entries.get(existing).object !== object) deleteWrapper(object);
            return existing;
        }

        const id = nextId;
        // The Unity i32 import passes opaque tokens as positive signed integers.
        if (id > 0x7fffffff) {
            deleteWrapper(object);
            throw new RangeError("FMOD opaque handle identities are exhausted.");
        }
        nextId++;
        entries.set(id, { object, kind, owner, native });
        system.ids.add(id);
        family.set(native, id);
        if (kind === "studio") nativeSystems.set(native, id);
        return id;
    }

    function get(id, kind) {
        const entry = entries.get(id);
        return entry && entry.kind === kind ? entry.object : null;
    }

    function owner(id) {
        const entry = entries.get(id);
        return entry ? entry.owner : 0;
    }

    // This only deletes a binding wrapper; the bridge owns the preceding native
    // unload/release operation. Retired tokens are never recycled in this module.
    function release(id) {
        const entry = entries.get(id);
        if (!entry) return;
        entries.delete(id);
        const system = systems.get(entry.owner);
        system.ids.delete(id);
        system.released.delete(id);
        system.families.get(entry.kind).delete(entry.native);
        if (entry.kind === "studio") nativeSystems.delete(entry.native);
        deleteWrapper(entry.object);
    }

    function markReleased(id) {
        const entry = entries.get(id);
        if (entry && entry.kind === "instance") systems.get(entry.owner).released.add(id);
    }

    function sweepReleased(systemId) {
        const system = systems.get(systemId);
        if (!system) return;
        // Scan only instances transferred back to FMOD, after Studio.update.
        // A playing released instance stays callable while its native handle lives.
        for (const id of system.released) {
            const entry = entries.get(id);
            if (!entry.object.isValid()) release(id);
        }
    }

    function releaseSystem(systemId) {
        const system = systems.get(systemId);
        if (!system) return;
        let failure;
        for (const id of system.ids) {
            if (id === systemId) continue;
            try {
                release(id);
            } catch (error) {
                failure = failure || error;
            }
        }
        try {
            release(systemId);
        } catch (error) {
            failure = failure || error;
        }
        systems.delete(systemId);
        if (failure) throw failure;
    }

    function dispose(releaseNative) {
        if (disposed) return;
        disposed = true;
        let failure;
        for (const systemId of systems.keys()) {
            const entry = entries.get(systemId);
            try {
                if (entry && entry.object.isValid()) {
                    const result = releaseNative ? releaseNative(entry.object, systemId) : entry.object.release();
                    if (result !== fmod.OK) throw new Error("FMOD Studio release failed: " + result + ".");
                }
            } catch (error) {
                failure = failure || error;
            }
            try {
                releaseSystem(systemId);
            } catch (error) {
                failure = failure || error;
            }
        }
        if (failure) throw failure;
    }

    return { put, get, owner, release, markReleased, sweepReleased, releaseSystem, dispose };
});
