// Converts the TypeScript container data classes in src/containers/*.ts into
// plain JSON files under data/containers/. The C# mod loads these at runtime.
//
// Each container file exports a single class whose public fields are pure data
// (arrays of item ids, preset builds, odds tables, etc.). We import the class,
// instantiate it, and serialise its own-enumerable properties to JSON.
//
// Requires Node >= 22.18 / 23 (native TypeScript type-stripping is enabled by
// default, so .ts files can be imported without a build step).

import { readdirSync, mkdirSync, writeFileSync } from "node:fs";
import { pathToFileURL } from "node:url";
import path from "node:path";

const containersDir = path.resolve("src/containers");
const outDir = path.resolve("data/containers");
mkdirSync(outDir, { recursive: true });

const files = readdirSync(containersDir).filter((f) => f.endsWith(".ts"));

let ok = 0;
let failed = 0;

for (const file of files) {
    const full = path.join(containersDir, file);
    try {
        const mod = await import(pathToFileURL(full).href);

        // Find the first exported class and instantiate it.
        const exportedClass = Object.values(mod).find(
            (v) => typeof v === "function"
        );
        if (!exportedClass) {
            console.warn(`SKIP ${file}: no exported class found`);
            failed++;
            continue;
        }

        const instance = new exportedClass();

        // Copy own-enumerable properties (the public data fields).
        const data = {};
        for (const key of Object.keys(instance)) {
            data[key] = instance[key];
        }

        const outName = path.basename(file, ".ts") + ".json";
        writeFileSync(
            path.join(outDir, outName),
            JSON.stringify(data, null, 2),
            "utf-8"
        );
        ok++;
    } catch (err) {
        console.error(`FAIL ${file}: ${err.message}`);
        failed++;
    }
}

console.log(`\nDone. Converted ${ok} files, ${failed} failed.`);
