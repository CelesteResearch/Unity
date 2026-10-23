import hashlib
import json
import pathlib
import re
import struct
import sys

root = pathlib.Path(__file__).resolve().parents[2]
input_path = pathlib.Path(sys.argv[1]).resolve()
catalog = json.loads(input_path.read_text(encoding="utf-8-sig"))
asset_root = root / "Assets"
runtime = asset_root / "Runtime/Graphics/SourceDevice/Effects"
resources = asset_root / "Resources/Celeste/Effects/Programs"
resource_directories = [
    asset_root / "Resources",
    asset_root / "Resources/Celeste",
    asset_root / "Resources/Celeste/Effects",
    resources,
]
new_resource_directories = [path for path in resource_directories if not path.exists()]
resources.mkdir(parents=True, exist_ok=True)


def stage_key(shader):
    profile = shader["profile"].encode()
    tokens = bytes.fromhex(shader["tokensHex"])
    wire = struct.pack("<I", len(profile)) + profile + struct.pack("<iI", shader["stage"], len(tokens)) + tokens
    wire += struct.pack("<I", len(shader["swizzles"]))
    for swizzle in shader["swizzles"]:
        wire += struct.pack("<iI4B", swizzle["usage"], swizzle["index"], *swizzle["values"])
    wire += struct.pack("<I", len(shader["samplerMap"]))
    for sampler in shader["samplerMap"]:
        wire += struct.pack("<ii", sampler["index"], sampler["type"])
    return hashlib.sha256(wire).hexdigest()


def key_pair(vertex, pixel):
    return hashlib.sha256((vertex + "|" + pixel).encode()).hexdigest()


def input_map(shader):
    result = []
    for value in shader["attributes"]:
        source = (value["usage"], value["index"])
        target = (5, 7) if source == (10, 1) else source
        result.append(
            {"sourceUsage": source[0], "sourceIndex": source[1], "unityUsage": target[0], "unityIndex": target[1]}
        )
    if len({(v["unityUsage"], v["unityIndex"]) for v in result}) != len(result):
        raise ValueError("Vertex semantic remapping collision")
    return result


def color_output_mask(shader):
    if shader["stage"] != 1:
        return 0
    mask = 0
    for output in shader["outputs"]:
        if output["usage"] == 10:
            if not 0 <= output["index"] < 4:
                raise ValueError("Pixel color output exceeds source attachment count")
            mask |= 1 << output["index"]
    return mask


def texture_bindings(shader):
    prefix = "VS" if shader["stage"] == 2 else "PS"
    return [
        {
            "stage": shader["stage"],
            "slot": s["index"],
            "type": s["type"],
            "property": f"_Celeste{prefix}Tex{s['index']}",
            "sampler": f"sampler_Celeste{prefix}Tex{s['index']}",
        }
        for s in shader["samplers"]
    ]


stages = {}
effects = {}
for effect in catalog["effects"]:
    effects[effect["name"]] = effect
    for shader in effect["shaders"]:
        shader["key"] = stage_key(shader)
        existing = stages.get(shader["key"])
        if existing is not None:
            # Logical entry/struct names may differ; register/semantic payload must not.
            for field in ("stage", "constantBufferBytes", "packing", "samplers", "attributes", "outputs"):
                if existing[field] != shader[field]:
                    raise ValueError("Identical shader key has different binding metadata: " + field)
        else:
            stages[shader["key"]] = shader

sprite_vertex = effects["SpriteEffect"]["shaders"][
    effects["SpriteEffect"]["techniques"][0]["passes"][0]["vertexChoices"][0]
]
pairs = {}
origins = []
unresolved = []
for effect in catalog["effects"]:
    for ti, technique in enumerate(effect["techniques"]):
        for pi, shader_pass in enumerate(technique["passes"]):
            if effect["name"] == "BasicEffect":
                choices = [
                    (effect["shaders"][p["vertex"]], effect["shaders"][p["pixel"]], p["shaderIndex"])
                    for p in effect["shaderIndexPairs"]
                ]
            else:
                if len(shader_pass["pixelChoices"]) != 1:
                    raise ValueError("Original pixel assignment coverage changed")
                if shader_pass["flags"] & 16:
                    vertex = sprite_vertex
                elif len(shader_pass["vertexChoices"]) == 1:
                    vertex = effect["shaders"][shader_pass["vertexChoices"][0]]
                else:
                    raise ValueError("Unresolved original vertex assignment")
                choices = [(vertex, effect["shaders"][shader_pass["pixelChoices"][0]], None)]
            for vertex, pixel, selector in choices:
                if vertex["stage"] != 2 or pixel["stage"] != 1:
                    raise ValueError("Invalid shader stage pair")
                outputs = {(v["usage"], v["index"]) for v in vertex["outputs"]}
                # Rasterizer-provided pixel inputs have no user varying with their Mojo usage ID.
                inputs = {
                    (0, 0) if v["name"] == "vPos" else (v["usage"], v["index"])
                    for v in pixel["attributes"]
                    if v["name"] != "vFace"
                }
                if not inputs.issubset(outputs):
                    unresolved.append(
                        {
                            "effect": effect["name"],
                            "technique": ti,
                            "pass": pi,
                            "shaderIndex": selector,
                            "missingSemantics": sorted(inputs - outputs),
                        }
                    )
                    continue
                key = key_pair(vertex["key"], pixel["key"])
                pairs[key] = (vertex, pixel)
                origins.append(
                    {
                        "effect": effect["name"],
                        "technique": ti,
                        "pass": pi,
                        "shaderIndex": selector,
                        "flags": shader_pass["flags"],
                        "program": key,
                    }
                )


def stage_source(shader):
    prefix = "VS" if shader["stage"] == 2 else "PS"
    source = shader["hlsl"].rstrip("\0")
    entry = shader["entry"]
    names = {
        entry: "Celeste" + prefix,
        entry + "_Input": "Celeste" + prefix + "Input",
        entry + "_Output": "Celeste" + prefix + "Output",
        entry + "_Uniforms": "Celeste" + prefix + "Uniforms",
        "uniforms_float4": "_Celeste" + prefix,
        "uniforms_int4": "_Celeste" + prefix + "Int",
        "uniforms_bool": "_Celeste" + prefix + "Bool",
    }
    for sampler in shader["samplers"]:
        name = sampler["name"]
        prop = f"_Celeste{prefix}Tex{sampler['index']}"
        names[name + "_texture"] = prop
        names[name] = "sampler" + prop
    # Register bindings are assigned by Unity's reflected ShaderLab binding route.
    source = re.sub(r"\s*:\s*register\([bst]\d+\)", "", source)
    source = re.sub(r"\b[A-Za-z_]\w*\b", lambda m: names.get(m[0], m[0]), source)
    if shader["stage"] == 2 and any(a["usage"] == 10 and a["index"] == 1 for a in shader["attributes"]):
        pattern = r"(struct CelesteVSInput\s*\{)(.*?)(\};)"
        source, changed = re.subn(
            pattern, lambda m: m[1] + re.sub(r"\bCOLOR1\b", "TEXCOORD7", m[2]) + m[3], source, flags=re.S
        )
        if changed != 1:
            raise ValueError("Lighting vertex input remap not located")
    return source


def linked_source(vertex, pixel):
    vertex_source = stage_source(vertex)
    pixel_source = stage_source(pixel)
    vertex_output = re.search(r"struct CelesteVSOutput\s*\{(.*?)\};", vertex_source, re.S)
    if vertex_output is None:
        raise ValueError("Vertex output structure missing")
    members = {}
    front_face = ""
    for attribute in pixel["attributes"]:
        if attribute["name"] == "vFace":
            front_face = "\tbool m_vFace : SV_IsFrontFace;\n"
            continue
        output = next(
            (
                v
                for v in vertex["outputs"]
                if (v["usage"], v["index"]) == (attribute["usage"], attribute["index"])
                or attribute["name"] == "vPos"
                and (v["usage"], v["index"]) == (0, 0)
            ),
            None,
        )
        if output is None:
            raise ValueError("Pixel input has no original matching vertex output")
        members["m_" + attribute["name"]] = "m_" + output["name"]
    # Match the pinned Mojo D3D11 linker: PS registers must occupy the complete
    # VS output ordering, including unused fields. Semantic labels alone do not
    # preserve the hardware varying register assignment for a subset PS input.
    pixel_source, changed = re.subn(
        r"(struct CelestePSInput\s*\{)(.*?)(\};)",
        lambda m: m[1] + vertex_output[1] + front_face + m[3],
        pixel_source,
        flags=re.S,
    )
    if changed != 1:
        raise ValueError("Pixel input structure missing")
    pixel_source = re.sub(r"\binput\.(m_[A-Za-z_]\w*)\b", lambda m: "input." + members.get(m[1], m[1]), pixel_source)
    # Source effects emit D3D clip z in [0, w]. Unity owns physical depth
    # direction/range, so adapt only the final position after original math.
    position = re.findall(r"\b(float4)\s+(m_[A-Za-z_]\w*)\s*:\s*SV_Position\s*;", vertex_output[1], re.I)
    if len(position) != 1:
        raise ValueError("Unique vertex SV_Position output missing")
    vertex_source, changed = re.subn(r"\bCelesteVS\s*\(", "CelesteSourceVS(", vertex_source)
    if changed != 1:
        raise ValueError("Unique source vertex entry missing")
    field = position[0][1]
    adapter = f"""
CelesteVSOutput CelesteVS(CelesteVSInput input)
{{
    CelesteVSOutput output = CelesteSourceVS(input);
#if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3)
    // Every source attachment, including the faux backbuffer, uses logical row zero at GL storage row zero.
    output.{field}.y = -output.{field}.y;
#endif
#if defined(UNITY_REVERSED_Z)
    output.{field}.z = output.{field}.w - output.{field}.z;
#elif defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3)
    output.{field}.z = 2.0 * output.{field}.z - output.{field}.w;
#endif
    return output;
}}
"""
    return vertex_source + "\n" + adapter + "\n" + pixel_source


integer_properties = {
    "_SrcBlend": 1,
    "_DstBlend": 0,
    "_SrcBlendAlpha": 1,
    "_DstBlendAlpha": 0,
    "_BlendOp": 0,
    "_BlendOpAlpha": 0,
    "_Cull": 0,
    "_ZWrite": 0,
    "_ZTest": 8,
    "_ColorMask": 15,
    "_StencilRef": 0,
    "_StencilReadMask": 255,
    "_StencilWriteMask": 0,
    "_StencilCompFront": 8,
    "_StencilCompBack": 8,
    "_StencilPassFront": 0,
    "_StencilPassBack": 0,
    "_StencilFailFront": 0,
    "_StencilFailBack": 0,
    "_StencilZFailFront": 0,
    "_StencilZFailBack": 0,
}
render_states = """            Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
            BlendOp [_BlendOp], [_BlendOpAlpha]
            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            ColorMask [_ColorMask]
            Offset [_SlopeScaleBias], [_DepthBias]
            Stencil
            {
                Ref [_StencilRef]
                ReadMask [_StencilReadMask]
                WriteMask [_StencilWriteMask]
                CompFront [_StencilCompFront]
                CompBack [_StencilCompBack]
                PassFront [_StencilPassFront]
                PassBack [_StencilPassBack]
                FailFront [_StencilFailFront]
                FailBack [_StencilFailBack]
                ZFailFront [_StencilZFailFront]
                ZFailBack [_StencilZFailBack]
            }
"""

for key, (vertex, pixel) in sorted(pairs.items()):
    props = [f'        {name} ("{name}", Integer) = {value}' for name, value in integer_properties.items()]
    props += ['        _DepthBias ("Depth bias", Float) = 0', '        _SlopeScaleBias ("Slope scale bias", Float) = 0']
    for sampler in texture_bindings(vertex) + texture_bindings(pixel):
        kind = {0: "2D", 1: "Cube", 2: "3D"}[sampler["type"]]
        props.append(f'        {sampler["property"]} ("{sampler["property"]}", {kind}) = "white" {{}}')
    states = render_states
    if color_output_mask(pixel) & ~1:
        props += [f'        _ColorMask{i} ("_ColorMask{i}", Integer) = 15' for i in range(1, 4)]
        masks = "            ColorMask [_ColorMask]" + "".join(
            f"\n            ColorMask [_ColorMask{i}] {i}" for i in range(1, 4)
        )
        states = states.replace("            ColorMask [_ColorMask]", masks)
    source = linked_source(vertex, pixel)
    output = (
        f'Shader "Celeste/Effects/{key}"\n{{\n    Properties\n    {{\n'
        + "\n".join(props)
        + "\n    }\n    SubShader\n    {\n        Pass\n        {\n"
        + states
        + '            HLSLPROGRAM\n            #pragma target 3.5\n            #pragma vertex CelesteVS\n            #pragma fragment CelestePS\n            #include "HLSLSupport.cginc"\n            // Keep source SM4 target semantics; the include also defines legacy DX9 aliases.\n            #undef SV_Target0\n'
        + source
        + "\n            ENDHLSL\n        }\n    }\n}\n"
    )
    (resources / (key + ".shader")).write_text(output, encoding="utf-8")


def quoted(value):
    return json.dumps(value, ensure_ascii=False)


stage_ids = {key: i for i, key in enumerate(sorted(stages))}
lines = [
    "namespace Celeste.Unity.Graphics.SourceDevice.Effects",
    "{",
    "    public static partial class UnityEffectShaderCatalog",
    "    {",
    "        public static readonly UnityEffectShaderStage[] Stages =",
    "        {",
]
for key, shader in sorted(stages.items()):
    packing = ", ".join(
        "new UnityEffectUniformPacking({type}, {index}, {count}, {byteOffset})".format(**p) for p in shader["packing"]
    )
    mapped = input_map(shader) if shader["stage"] == 2 else []
    inputs = ", ".join(
        "new UnityEffectShaderInput({sourceUsage}, {sourceIndex}, {unityUsage}, {unityIndex})".format(**v)
        for v in mapped
    )
    textures = ", ".join(
        f"new UnityEffectShaderTextureBinding({v['stage']}, {v['slot']}, {v['type']}, {quoted(v['property'])}, {quoted(v['sampler'])})"
        for v in texture_bindings(shader)
    )
    lines.append(
        f'            new UnityEffectShaderStage("{key}", {shader["stage"]}, {shader["constantBufferBytes"]}, new UnityEffectUniformPacking[] {{ {packing} }}, new UnityEffectShaderInput[] {{ {inputs} }}, new UnityEffectShaderTextureBinding[] {{ {textures} }}, {color_output_mask(shader)}u),'
    )
lines += ["        };", "", "        public static readonly UnityEffectShaderProgram[] Programs =", "        {"]
for key, (vertex, pixel) in sorted(pairs.items()):
    lines.append(
        f'            new UnityEffectShaderProgram("{key}", Stages[{stage_ids[vertex["key"]]}], Stages[{stage_ids[pixel["key"]]}]),'
    )
lines += ["        };", "    }", "}"]
(runtime / "UnityEffectShaderCatalog.Generated.cs").write_text("\n".join(lines) + "\n", encoding="utf-8")

manifest = {
    "sourceSha256": hashlib.sha256(input_path.read_bytes()).hexdigest(),
    "profile": "hlsl",
    "effectCount": len(effects),
    "unresolved": unresolved,
    "stageCount": len(stages),
    "programCount": len(pairs),
    "origins": origins,
    "stages": [
        {
            "key": key,
            "stage": s["stage"],
            "constantBufferBytes": s["constantBufferBytes"],
            "packing": s["packing"],
            "inputs": input_map(s) if s["stage"] == 2 else [],
            "textures": texture_bindings(s),
            "colorOutputMask": color_output_mask(s),
        }
        for key, s in sorted(stages.items())
    ],
    "programs": [
        {"key": key, "vertex": v["key"], "pixel": p["key"], "resource": "Celeste/Effects/Programs/" + key}
        for key, (v, p) in sorted(pairs.items())
    ],
}
(root / "Manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")


def write_metadata(paths):
    for path in sorted(paths):
        if not path.exists():
            continue
        relative = path.relative_to(asset_root).as_posix()
        if pathlib.Path(str(path) + ".meta").exists():
            continue
        guid = hashlib.sha256(("Celeste.Unity/" + relative).encode()).hexdigest()[:32]
        if path.is_dir():
            meta = f"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
        elif path.suffix == ".cs":
            meta = f"fileFormatVersion: 2\nguid: {guid}\nMonoImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {{instanceID: 0}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
        elif path.suffix == ".shader":
            meta = f"fileFormatVersion: 2\nguid: {guid}\nShaderImporter:\n  externalObjects: {{}}\n  defaultTextures: []\n  nonModifiableTextures: []\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
        else:
            raise ValueError("Unexpected owned metadata output: " + relative)
        pathlib.Path(str(path) + ".meta").write_text(meta, encoding="utf-8")


runtime_names = (
    "UnityEffectUniformPacking.cs",
    "UnityEffectShaderInput.cs",
    "UnityEffectShaderTextureBinding.cs",
    "UnityEffectShaderStage.cs",
    "UnityEffectShaderProgram.cs",
    "UnityEffectShaderCatalog.cs",
    "UnityEffectShaderCatalog.Generated.cs",
)
owned_outputs = [runtime / name for name in runtime_names] + [resources / (key + ".shader") for key in pairs]
write_metadata(owned_outputs + [runtime] + new_resource_directories)
print(
    json.dumps(
        {
            "effects": len(effects),
            "uniqueStages": len(stages),
            "uniquePrograms": len(pairs),
            "origins": len(origins),
            "unresolved": unresolved,
        }
    )
)
