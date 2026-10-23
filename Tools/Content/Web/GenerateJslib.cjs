"use strict";
const fs = require("node:fs"),
    path = require("node:path");
const source = fs.readFileSync(path.join(__dirname, "VerifiedContentCache.js"), "utf8");
const output =
    process.argv[2] || path.join(__dirname, "../../../Assets/Plugins/WebGL/CelesteVerifiedContentCache.jslib");
const postset = "(function(){" + source + "\nCelesteVerifiedContentCache=createVerifiedContentCache();})();";
fs.mkdirSync(path.dirname(output), { recursive: true });
fs.writeFileSync(
    output,
    "mergeInto(LibraryManager.library, {\n    $CelesteVerifiedContentCache: {},\n    $CelesteVerifiedContentCache__postset: " +
        JSON.stringify(postset) +
        "\n});\n",
);
console.log("VERIFIED_CONTENT_CACHE_GENERATED");
