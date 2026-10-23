"use strict";
const fs = require("node:fs");
const path = require("node:path");
const source = fs.readFileSync(path.join(__dirname, "FullContentDownload.js"), "utf8");
const output =
    process.argv[2] || path.join(__dirname, "../../../Assets/Plugins/WebGL/CelesteFullContentDownload.jslib");
const postset = "(function(){" + source + "\nCelesteFullContentDownloadFactory=createFullContentDownload;})();";
fs.mkdirSync(path.dirname(output), { recursive: true });
fs.writeFileSync(
    output,
    "mergeInto(LibraryManager.library, {\n    $CelesteFullContentDownloadFactory: {},\n    $CelesteFullContentDownloadFactory__postset: " +
        JSON.stringify(postset) +
        "\n});\n",
);
console.log("FULL_CONTENT_DOWNLOAD_GENERATED");
