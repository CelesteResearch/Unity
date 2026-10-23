"use strict";
const fs = require("node:fs");
const path = require("node:path");
const crypto = require("node:crypto");
const pageSize = 1024 * 1024;
const names = [
    "Master Bank.bank",
    "Master Bank.strings.bank",
    "music.bank",
    "sfx.bank",
    "ui.bank",
    "dlc_music.bank",
    "dlc_sfx.bank",
];
function build(contentRoot) {
    const banks = [];
    for (const name of names) {
        const logical = "FMOD/Desktop/" + name;
        const file = path.join(contentRoot, logical);
        const size = fs.statSync(file).size;
        const fd = fs.openSync(file, "r");
        const full = crypto.createHash("sha256");
        const pages = [];
        try {
            for (let offset = 0; offset < size; offset += pageSize) {
                const bytes = Buffer.allocUnsafe(Math.min(pageSize, size - offset));
                let filled = 0;
                while (filled < bytes.length) {
                    const count = fs.readSync(fd, bytes, filled, bytes.length - filled, offset + filled);
                    if (!count) throw Error("Unexpected bank EOF: " + logical);
                    filled += count;
                }
                full.update(bytes);
                pages.push(crypto.createHash("sha256").update(bytes).digest("hex"));
            }
        } finally {
            fs.closeSync(fd);
        }
        banks.push({ path: logical, size, sha256: full.digest("hex"), pages });
    }
    return { version: 1, pageSize, banks };
}
module.exports = { build };
if (require.main === module) {
    const [contentRoot, output, capturePath] = process.argv.slice(2);
    if (!contentRoot || !output) throw Error("Usage: node BuildBankPageIndex.cjs <content-root> <output-json>");
    const index = build(path.resolve(contentRoot));
    if (capturePath) {
        const capture = JSON.parse(fs.readFileSync(capturePath, "utf8"));
        if (!capture.passed || capture.sdk !== "2.03.15 P" || capture.pageSize !== pageSize) {
            throw Error("Startup capture is incompatible.");
        }
        for (let i = 0; i < index.banks.length; i++) {
            const bank = index.banks[i],
                observed = capture.banks[i];
            if (
                !observed ||
                bank.path !== observed.path ||
                bank.size !== observed.size ||
                bank.sha256 !== observed.sha256
            ) {
                throw Error("Startup capture bank identity mismatch.");
            }
            if (
                !observed.startupPages.every((page) => Number.isInteger(page) && page >= 0 && page < bank.pages.length)
            ) {
                throw Error("Startup capture page invalid.");
            }
            bank.startupPages = [...new Set(observed.startupPages)].sort((a, b) => a - b);
        }
        index.startupSdk = capture.sdk;
        index.startupLibrarySha256 = capture.librarySha256;
    }
    fs.writeFileSync(path.resolve(output), JSON.stringify(index, null, 2) + "\n");
    console.log(
        JSON.stringify({
            banks: index.banks.length,
            pages: index.banks.reduce((sum, bank) => sum + bank.pages.length, 0),
            bytes: index.banks.reduce((sum, bank) => sum + bank.size, 0),
            pageSize: index.pageSize,
        }),
    );
}
