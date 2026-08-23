import assert from "node:assert/strict";
import fs from "node:fs";

const moduleSource = fs.readFileSync(
    new URL("../../src/Emby.ExternalPlayer/Resources/external-player.js", import.meta.url),
    "utf8");
const detailPreview = fs.readFileSync(
    new URL("../visual/external-player-preview.html", import.meta.url),
    "utf8");
const configurationPreview = fs.readFileSync(
    new URL("../visual/external-player-config-preview.html", import.meta.url),
    "utf8");

assert.match(
    moduleSource,
    /define\(\s*\[\s*"\.\/language\.js",\s*"events",\s*"connectionManager"\s*\]/,
    "the visual fixtures must track the production AMD dependency contract");

for (const [name, preview] of [
    ["detail", detailPreview],
    ["configuration", configurationPreview]
]) {
    assert.match(preview, /const languageModule\s*=/, `${name} preview must provide the language module`);
    assert.match(
        preview,
        /factory\(languageModule,\s*events,\s*connectionManager\)/,
        `${name} preview must pass AMD dependencies in production order`);
    assert.doesNotMatch(preview, /v=1\.6\.12/, `${name} preview must not pin a stale resource version`);
    assert.match(preview, /id="previewStatus"/, `${name} preview must expose initialization failures`);
}

console.log("Visual preview fixture tests passed.");
