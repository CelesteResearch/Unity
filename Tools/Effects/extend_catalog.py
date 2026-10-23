"""Append compiled effects from selected external Mod archives to a cooked base catalog.

Uses the same CelesteGraphicsEffects ABI as the runtime. The native frontend owns
FX parsing and HLSL translation; this tool only serializes its descriptors.
"""

import argparse
import ctypes as c
import hashlib
import json
import pathlib
import zipfile

P, U, I = c.c_void_p, c.c_uint32, c.c_int32


class Description(c.Structure):
    _fields_ = [
        ("profile", P),
        ("entryPoint", P),
        ("tokens", P),
        ("tokenLength", U),
        ("swizzles", P),
        ("swizzleCount", U),
        ("samplerMap", P),
        ("samplerCount", U),
        ("parsed", P),
        ("floatCount", I),
        ("intCount", I),
        ("boolCount", I),
        ("stage", I),
        ("output", P),
        ("outputLength", I),
        ("outputEntryPoint", P),
        ("uniforms", P),
        ("uniformCount", I),
        ("samplers", P),
        ("samplerCountParsed", I),
        ("attributes", P),
        ("attributeCount", I),
        ("outputs", P),
        ("outputCount", I),
        ("identity", P),
        ("packing", P),
        ("packingCount", I),
        ("constantBufferBytes", I),
    ]


class Swizzle(c.Structure):
    _fields_ = [("usage", I), ("index", U), ("values", c.c_uint8 * 4)]


class SamplerMap(c.Structure):
    _fields_ = [("index", I), ("type", I)]


class Packing(c.Structure):
    _fields_ = [("type", I), ("index", I), ("count", I), ("byteOffset", I)]


class Sampler(c.Structure):
    _fields_ = [("type", I), ("index", I), ("name", c.c_char_p), ("texbem", I)]


class Attribute(c.Structure):
    _fields_ = [("usage", I), ("index", I), ("name", c.c_char_p)]


def records(pointer, count, kind, fields):
    values = c.cast(pointer, c.POINTER(kind))
    result = []
    for index in range(count):
        row = {}
        for field in fields:
            value = getattr(values[index], field)
            row[field] = (
                value.decode() if isinstance(value, bytes) else list(value) if isinstance(value, c.Array) else value
            )
        result.append(row)
    return result


class Frontend:
    def __init__(self, library):
        self.dll = c.CDLL(str(library))
        signatures = {
            "GetError": ([], c.c_char_p),
            "FrontendCreate": ([c.c_char_p, c.POINTER(P)], I),
            "FrontendDestroy": ([P], None),
            "Create": ([P, P, U, c.POINTER(P), c.POINTER(P)], I),
            "Destroy": ([P], None),
            "GetShaderCount": ([P, c.POINTER(U)], I),
            "AcquireShader": ([P, U, c.POINTER(P)], I),
            "ReleaseShader": ([P], None),
            "GetShaderDescription": ([P, c.POINTER(Description)], I),
            "GetTechniqueCount": ([P, c.POINTER(U)], I),
            "GetTechnique": ([P, U, c.POINTER(P), c.POINTER(U)], I),
            "GetPassStageFlags": ([P, U, U, c.POINTER(U)], I),
            "GetPassShaderCount": ([P, U, U, I, c.POINTER(U)], I),
            "AcquirePassShader": ([P, U, U, I, U, c.POINTER(P)], I),
        }
        for name, (arguments, result) in signatures.items():
            function = getattr(self.dll, "CelesteEffect" + name)
            function.argtypes, function.restype = arguments, result
            setattr(self, name, function)
        self.handle = P()
        self.check(self.FrontendCreate(b"hlsl", c.byref(self.handle)))

    def check(self, result):
        if not result:
            raise RuntimeError(self.GetError().decode())

    def description(self, lease):
        value = Description()
        self.check(self.GetShaderDescription(lease, c.byref(value)))
        return value

    def shader(self, value, ordinal):
        return {
            "ordinal": ordinal,
            "stage": value.stage,
            "entry": c.string_at(value.outputEntryPoint).decode(),
            "profile": c.string_at(value.profile).decode(),
            "tokensHex": c.string_at(value.tokens, value.tokenLength).hex(),
            "swizzles": records(value.swizzles, value.swizzleCount, Swizzle, ("usage", "index", "values")),
            "samplerMap": records(value.samplerMap, value.samplerCount, SamplerMap, ("index", "type")),
            "constantBufferBytes": value.constantBufferBytes,
            "packing": records(value.packing, value.packingCount, Packing, ("type", "index", "count", "byteOffset")),
            "samplers": records(value.samplers, value.samplerCountParsed, Sampler, ("index", "type", "name")),
            "attributes": records(value.attributes, value.attributeCount, Attribute, ("usage", "index", "name")),
            "outputs": records(value.outputs, value.outputCount, Attribute, ("usage", "index", "name")),
            "hlsl": c.string_at(value.output, value.outputLength).decode(),
        }

    def effect(self, name, code):
        effect, metadata = P(), P()
        buffer = c.create_string_buffer(code)
        self.check(self.Create(self.handle, buffer, len(code), c.byref(effect), c.byref(metadata)))
        try:
            shaders, identities, techniques = [], {}, []
            count = U()
            self.check(self.GetShaderCount(effect, c.byref(count)))
            for ordinal in range(count.value):
                lease = P()
                self.check(self.AcquireShader(effect, ordinal, c.byref(lease)))
                try:
                    description = self.description(lease)
                    identities[description.identity] = ordinal
                    shaders.append(self.shader(description, ordinal))
                finally:
                    self.ReleaseShader(lease)
            self.check(self.GetTechniqueCount(effect, c.byref(count)))
            for technique in range(count.value):
                pointer, passes = P(), U()
                self.check(self.GetTechnique(effect, technique, c.byref(pointer), c.byref(passes)))
                rows = []
                for index in range(passes.value):
                    flags = U()
                    self.check(self.GetPassStageFlags(effect, technique, index, c.byref(flags)))
                    row = {"flags": flags.value}
                    for vertex, key in ((1, "vertexChoices"), (0, "pixelChoices")):
                        choices, choice_count = [], U()
                        self.check(self.GetPassShaderCount(effect, technique, index, vertex, c.byref(choice_count)))
                        for choice in range(choice_count.value):
                            lease = P()
                            self.check(self.AcquirePassShader(effect, technique, index, vertex, choice, c.byref(lease)))
                            try:
                                choices.append(identities[self.description(lease).identity])
                            finally:
                                self.ReleaseShader(lease)
                        row[key] = choices
                    rows.append(row)
                techniques.append({"passes": rows})
            return {
                "name": name,
                "stock": False,
                "bytes": len(code),
                "sha256": hashlib.sha256(code).hexdigest(),
                "shaders": shaders,
                "techniques": techniques,
            }
        finally:
            self.Destroy(effect)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-catalog", required=True, type=pathlib.Path)
    parser.add_argument("--native-library", required=True, type=pathlib.Path)
    parser.add_argument("--mod", required=True, type=pathlib.Path, action="append")
    parser.add_argument("--output", required=True, type=pathlib.Path)
    args = parser.parse_args()
    catalog = json.loads(args.base_catalog.read_text(encoding="utf-8-sig"))
    names = {effect["name"] for effect in catalog["effects"]}
    frontend = Frontend(args.native_library.resolve())
    appended = []
    try:
        for archive in args.mod:
            with zipfile.ZipFile(archive) as package:
                for resource in sorted(package.namelist()):
                    if not resource.lower().endswith((".cso", ".fxb")):
                        continue
                    name = archive.name + ":" + resource
                    if name in names:
                        raise ValueError("Duplicate effect catalog identity: " + name)
                    effect = frontend.effect(name, package.read(resource))
                    catalog["effects"].append(effect)
                    appended.append({"name": name, "sha256": effect["sha256"], "stages": len(effect["shaders"])})
                    names.add(name)
    finally:
        frontend.FrontendDestroy(frontend.handle)
    args.output.write_text(json.dumps(catalog, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps({"output": str(args.output), "appended": appended}))


if __name__ == "__main__":
    main()
