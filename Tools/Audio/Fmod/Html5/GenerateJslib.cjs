"use strict";
const fs = require("node:fs");
const path = require("node:path");
const dir = __dirname;
const projectDir = path.resolve(dir, "../../../..");
const nativeDir = process.argv[2]
    ? path.resolve(process.argv[2])
    : path.join(projectDir, "Assets/Runtime/Audio/Fmod/Html5");
const outputFile = process.argv[3]
    ? path.resolve(process.argv[3])
    : path.join(projectDir, "Assets/Plugins/WebGL/CelesteFmodHtml5.jslib");
const declarations = [
    "FmodHtml5NativeMethods.cs",
    "FmodHtml5NativeMethods.Lifecycle.cs",
    "FmodHtml5NativeMethods.FilePreparation.cs",
    "FmodHtml5NativeMethods.FileProgress.cs",
]
    .map((file) => fs.readFileSync(path.join(nativeDir, file), "utf8"))
    .join("\n");
const entries = [];
for (const match of declarations.matchAll(
    /internal\s+static\s+extern\s+(?:FmodResult|void)\s+(\w+)\s*\(([^;]*?)\);/g,
)) {
    if (["Load", "CancelLoad", "DisposeModule", "Free", "PrepareBankFile", "CancelPrepareBankFile"].includes(match[1]))
        continue;
    const raw = match[2].replace(/\[[^\]]*\]/g, "").split(",");
    const parameters = raw.map((p) => p.trim().split(/\s+/).at(-1).replace(/^@/, ""));
    entries.push({ name: match[1], parameters });
}
if (new Set(entries.map((e) => e.name)).size !== 73) throw new Error("Expected 73 physical FMOD entrypoints.");
const handles = fs.readFileSync(path.join(dir, "ThreadedFmodSdkHandles.js"), "utf8");
const bridge = fs.readFileSync(path.join(dir, "ThreadedFmodStudioBridge.js"), "utf8");
const bankService = fs.readFileSync(path.join(dir, "FmodBankFileService.js"), "utf8");
const bankIndex = JSON.parse(fs.readFileSync(path.join(dir, "OriginalBankPages.json"), "utf8"));
for (const bank of bankIndex.banks) {
    if (
        !Array.isArray(bank.startupPages) ||
        bank.startupPages.some((page) => !Number.isInteger(page) || page < 0 || page >= bank.pages.length)
    )
        throw new Error("The bank index must contain measured startup pages.");
}
const postset = `(function(){${handles}\n${bankService}\n${bridge}\nCelesteFmodBridge=createThreadedFmodStudioBridge({u8:()=>HEAPU8,i32:()=>HEAP32,u32:()=>HEAPU32,f32:()=>HEAPF32,f64:()=>HEAPF64,readUtf8:UTF8ToString,writeUtf8:stringToUTF8,utf8Length:lengthBytesUTF8,allocateUtf8:function(text){var size=lengthBytesUTF8(text)+1;var ptr=_malloc(size);if(!ptr)throw new Error('Unity UTF8 allocation failed.');stringToUTF8(text,ptr,size);return ptr;},free:function(pointer){_free(pointer);}},ThreadedFmodSdkHandles.create,createFmodBankFileService,${JSON.stringify(bankIndex)});})();`;
const lines = [
    "mergeInto(LibraryManager.library, {",
    "    $CelesteFmodBridge: {},",
    '    $CelesteFmodBridge__deps: ["$UTF8ToString", "$stringToUTF8", "$lengthBytesUTF8", "malloc", "free"],',
    "    $CelesteFmodBridge__postset: " + JSON.stringify(postset) + ",",
];
lines.push('    CelesteFmod_Load__deps: ["$CelesteFmodBridge"],');
lines.push(
    "    CelesteFmod_Load: function(request, scriptUrl, wasmUrl, callback) { CelesteFmodBridge.load(request, UTF8ToString(scriptUrl), UTF8ToString(wasmUrl), function(requestId,moduleId,error) { {{{ makeDynCall("viii", "callback") }}}(requestId,moduleId,error); }); },",
);
lines.push('    CelesteFmod_PrepareBankFile__deps: ["$CelesteFmodBridge"],');
lines.push(
    "    CelesteFmod_PrepareBankFile: function(request, module, path, url, size, sha256, callback) { CelesteFmodBridge.prepareBankFile(request, module, UTF8ToString(path), UTF8ToString(url), size, UTF8ToString(sha256), function(requestId,error) { {{{ makeDynCall("vii", "callback") }}}(requestId,error); }); },",
);
for (const [name, method, args] of [
    ["CancelLoad", "cancelLoad", "request"],
    ["DisposeModule", "disposeModule", "module"],
    ["Free", "free", "module,pointer"],
    ["CancelPrepareBankFile", "cancelPrepareBankFile", "request"],
]) {
    lines.push(`    CelesteFmod_${name}__deps: ["$CelesteFmodBridge"],`);
    lines.push(`    CelesteFmod_${name}: function(${args}) { CelesteFmodBridge.${method}(${args}); },`);
}
for (const entry of entries) {
    const args = entry.parameters.join(", ");
    lines.push(`    CelesteFmod_${entry.name}__deps: ["$CelesteFmodBridge"],`);
    lines.push(
        `    CelesteFmod_${entry.name}: function(${args}) { return CelesteFmodBridge.${entry.name}(${args}); },`,
    );
}
lines.push("});", "");
fs.writeFileSync(outputFile, lines.join("\n"));
console.log("JSLIB_GENERATED_ENTRYPOINTS", entries.length + 6);
