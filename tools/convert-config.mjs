// Converts config/config.jsonc -> config/config.json (strips comments) and
// generates data/container-info.json from MysteryContainerInfo.ts using the
// parsed config so container metadata (ids, prefabs, sizes, barters, localized
// descriptions) is available to the C# mod as plain JSON.

import { readFileSync, writeFileSync } from "node:fs";
import { pathToFileURL } from "node:url";
import path from "node:path";

/** Strip // line comments and block comments from JSONC, respecting strings. */
function stripJsonComments(text) {
    let out = "";
    let inString = false;
    let inLineComment = false;
    let inBlockComment = false;
    for (let i = 0; i < text.length; i++) {
        const ch = text[i];
        const next = text[i + 1];

        if (inLineComment) {
            if (ch === "\n") {
                inLineComment = false;
                out += ch;
            }
            continue;
        }
        if (inBlockComment) {
            if (ch === "*" && next === "/") {
                inBlockComment = false;
                i++;
            }
            continue;
        }
        if (inString) {
            out += ch;
            if (ch === "\\") {
                out += text[i + 1] ?? "";
                i++;
            } else if (ch === '"') {
                inString = false;
            }
            continue;
        }
        if (ch === '"') {
            inString = true;
            out += ch;
            continue;
        }
        if (ch === "/" && next === "/") {
            inLineComment = true;
            i++;
            continue;
        }
        if (ch === "/" && next === "*") {
            inBlockComment = true;
            i++;
            continue;
        }
        out += ch;
    }
    return out;
}

/** Remove trailing commas that JSON.parse rejects. */
function removeTrailingCommas(text) {
    return text.replace(/,(\s*[}\]])/g, "$1");
}

const jsoncPath = path.resolve("config/config.jsonc");
const raw = readFileSync(jsoncPath, "utf-8");
const cleaned = removeTrailingCommas(stripJsonComments(raw));

let config;
try {
    config = JSON.parse(cleaned);
} catch (err) {
    console.error("Failed to parse config.jsonc after cleaning:", err.message);
    process.exit(1);
}

writeFileSync(
    path.resolve("config/config.json"),
    JSON.stringify(config, null, 2),
    "utf-8"
);
console.log("Wrote config/config.json");

// Generate container-info.json from MysteryContainerInfo(config)
const infoModule = await import(
    pathToFileURL(path.resolve("src/MysteryContainerInfo.ts")).href
);
const info = infoModule.MysteryContainerInfo(config);
writeFileSync(
    path.resolve("data/container-info.json"),
    JSON.stringify(info, null, 2),
    "utf-8"
);
console.log(`Wrote data/container-info.json (${Object.keys(info).length} containers)`);
