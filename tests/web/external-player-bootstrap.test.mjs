import assert from "node:assert/strict";
import fs from "node:fs";
import vm from "node:vm";

const listeners = new Map();
const document = {
    addEventListener(name, handler) {
        listeners.set(name, handler);
    },
    removeEventListener(name, handler) {
        if (listeners.get(name) === handler) {
            listeners.delete(name);
        }
    }
};
const stored = new Map();
const sessionStorage = {
    getItem(key) {
        return stored.get(key) || null;
    },
    setItem(key, value) {
        stored.set(key, value);
    }
};
const fetchCalls = [];
let reloads = 0;
let requireCalls = 0;
const context = {
    console: { warn() {} },
    document,
    Emby: {},
    location: {
        pathname: "/web/index.html",
        reload() { reloads++; }
    },
    sessionStorage,
    urlCacheParam: "v=4.9.5.0",
    clearTimeout,
    setTimeout,
    fetch(url, options) {
        fetchCalls.push({ url, options });
        return Promise.resolve({
            ok: true,
            status: 200,
            arrayBuffer() {
                return Promise.resolve(new ArrayBuffer(0));
            }
        });
    },
    require() {
        requireCalls++;
        throw new Error("The cache helper must not load the feature module directly.");
    }
};
context.globalThis = context;

const source = fs.readFileSync(
    new URL("../../src/Emby.ExternalPlayer/Resources/external-player-bootstrap.js", import.meta.url),
    "utf8");
vm.runInNewContext(source, context, { filename: "external-player-bootstrap.js" });

assert.equal(fetchCalls.length, 0, "cache repair must wait until Emby's plugin phase has completed");
listeners.get("appready")();
await new Promise((resolve) => setTimeout(resolve, 0));

assert.equal(requireCalls, 0, "button code must continue to load through Emby's app.js plugin list");
assert.equal(fetchCalls.length, 1, "a missing feature module should refresh the stale app.js response once");
assert.equal(fetchCalls[0].url, "./app.js?v=4.9.5.0");
assert.equal(fetchCalls[0].options.cache, "reload");
assert.equal(fetchCalls[0].options.credentials, "same-origin");
assert.equal(stored.size, 1);
assert.equal(reloads, 1, "the page must reload after app.js has been refreshed");

context.__embyExternalPlayerModule = { installed: true };
context.Emby.Page = {};
vm.runInNewContext(source, context, { filename: "external-player-bootstrap.js" });
await new Promise((resolve) => setTimeout(resolve, 0));

assert.equal(fetchCalls.length, 1, "a normally loaded feature module must bypass cache repair");
assert.equal(reloads, 1);
assert.equal(requireCalls, 0);

context.__embyExternalPlayerBootstrap.dispose();
assert.equal(listeners.has("appready"), false);

context.__embyExternalPlayerModule = undefined;
context.Emby = {};
context.sessionStorage = {
    getItem() { throw new Error("blocked"); },
    setItem() { throw new Error("blocked"); }
};
vm.runInNewContext(source, context, { filename: "external-player-bootstrap.js" });
listeners.get("appready")();
await new Promise((resolve) => setTimeout(resolve, 0));

assert.equal(fetchCalls.length, 2, "cache warming should remain available in private browsing");
assert.equal(reloads, 1, "blocked session storage must not cause a reload loop");

context.__embyExternalPlayerBootstrap.dispose();

console.log("External Player bootstrap tests passed.");
