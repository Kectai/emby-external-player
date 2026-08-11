import assert from "node:assert/strict";
import fs from "node:fs";
import vm from "node:vm";

class FakeElement {
    constructor(tagName, document) {
        this.tagName = tagName.toUpperCase();
        this.ownerDocument = document;
        this.children = [];
        this.parentNode = null;
        this.attributes = new Map();
        this.listeners = new Map();
        this.className = "";
        this.id = "";
        this.hidden = false;
        this.disabled = false;
        this.selected = false;
        this.type = "";
        this._textContent = "";
        this._value = "";
    }

    appendChild(child) {
        child.parentNode = this;
        this.children.push(child);
        if (this.tagName === "SELECT" && child.tagName === "OPTION" &&
            (child.selected || this.children.length === 1)) {
            this._value = child.value;
        }
        return child;
    }

    insertBefore(child, reference) {
        child.parentNode = this;
        const index = this.children.indexOf(reference);
        this.children.splice(index < 0 ? this.children.length : index, 0, child);
        return child;
    }

    removeChild(child) {
        this.children = this.children.filter((candidate) => candidate !== child);
        child.parentNode = null;
        return child;
    }

    remove() {
        if (this.parentNode) {
            this.parentNode.removeChild(this);
        }
    }

    setAttribute(name, value) {
        this.attributes.set(name, String(value));
    }

    addEventListener(name, handler) {
        const listeners = this.listeners.get(name) || [];
        listeners.push(handler);
        this.listeners.set(name, listeners);
    }

    removeEventListener(name, handler) {
        const listeners = this.listeners.get(name) || [];
        this.listeners.set(name, listeners.filter((candidate) => candidate !== handler));
    }

    dispatch(name, event = {}) {
        event.target ||= this;
        for (const handler of this.listeners.get(name) || []) {
            handler(event);
        }
    }

    focus() {
        this.ownerDocument.activeElement = this;
    }

    querySelector(selector) {
        if (selector === "button.btnPlay, button.btnResume, .btnPlay, .btnResume") {
            return this.walk().find((item) => item.className.includes("btnPlay") || item.className.includes("btnResume")) || null;
        }
        if (selector === "button") {
            return this.walk().find((item) => item.tagName === "BUTTON") || null;
        }
        return null;
    }

    querySelectorAll(selector) {
        if (selector.includes("button:not([disabled])")) {
            return this.walk().filter((item) =>
                !item.disabled &&
                (item.tagName === "BUTTON" || item.tagName === "SELECT" || item.tagName === "INPUT" ||
                 (item.tagName === "A" && item.href)));
        }
        return [];
    }

    walk() {
        return this.children.flatMap((child) => [child, ...child.walk()]);
    }

    get options() {
        return this.children.filter((child) => child.tagName === "OPTION");
    }

    get value() {
        return this._value;
    }

    set value(value) {
        this._value = String(value);
    }

    get textContent() {
        return this._textContent;
    }

    set textContent(value) {
        this._textContent = String(value);
        if (value === "") {
            this.children = [];
        }
    }
}

class FakeDocument {
    constructor() {
        this.listeners = new Map();
        this.head = new FakeElement("head", this);
        this.body = new FakeElement("body", this);
        this.activeElement = this.body;
        this.actionRow = new FakeElement("div", this);
        this.actionRow.className = "mainDetailButtons";
        const play = new FakeElement("button", this);
        play.className = "btnPlay";
        this.actionRow.appendChild(play);
        this.body.appendChild(this.actionRow);
    }

    createElement(tagName) {
        return new FakeElement(tagName, this);
    }

    getElementById(id) {
        return [this.head, this.body, ...this.head.walk(), ...this.body.walk()]
            .find((item) => item.id === id) || null;
    }

    querySelector(selector) {
        if (selector.startsWith(".mainDetailButtons")) {
            return this.actionRow;
        }
        if (selector === ".emby-external-player-overlay") {
            return this.body.walk().find((item) => item.className === "emby-external-player-overlay") || null;
        }
        return null;
    }

    addEventListener(name, handler) {
        const listeners = this.listeners.get(name) || [];
        listeners.push(handler);
        this.listeners.set(name, listeners);
    }

    removeEventListener(name, handler) {
        const listeners = this.listeners.get(name) || [];
        this.listeners.set(name, listeners.filter((candidate) => candidate !== handler));
    }

    dispatch(name, event) {
        for (const handler of this.listeners.get(name) || []) {
            handler(event);
        }
    }
}

const manifest = {
    Enabled: true,
    ItemId: "item-1",
    ItemName: "中文 Movie \"One\" 🎬",
    ButtonText: "外部播放",
    ButtonPlacement: "AfterPrimaryPlay",
    ResumeByDefault: true,
    ResumePositionTicks: 900000000,
    MediaSources: [{
        Id: "source-1",
        Name: "4K REMUX",
        IsDefault: true,
        Subtitles: [{ Index: 3, DisplayTitle: "简体中文 ASS", IsDefault: true }]
    }],
    Players: [{ Id: "Iina", DisplayName: "IINA" }]
};

const source = fs.readFileSync(
    new URL("../../src/Emby.ExternalPlayer/Resources/external-player.js", import.meta.url),
    "utf8");
const document = new FakeDocument();
const eventSubscriptions = new Set();
const events = {
    on(_source, name, handler) { eventSubscriptions.add(`${name}:${String(handler)}`); },
    off(_source, name, handler) { eventSubscriptions.delete(`${name}:${String(handler)}`); }
};
const apiClient = {
    getUrl(path) { return `http://127.0.0.1:18095/${path}`; },
    getJSON() { return Promise.resolve(manifest); },
    ajax() { return Promise.resolve({ LaunchUrl: "iina://weblink?url=https%3A%2F%2Fexample.test" }); }
};
const connectionManager = { currentApiClient() { return apiClient; } };
const windowListeners = new Map();
const window = {
    document,
    location: { hash: "#!/item?id=item-1", search: "", href: "" },
    addEventListener(name, handler) { windowListeners.set(name, handler); },
    removeEventListener(name, handler) {
        if (windowListeners.get(name) === handler) windowListeners.delete(name);
    },
    setTimeout,
    clearTimeout
};
let initializer;
const sandbox = {
    window,
    document,
    navigator: { platform: "MacIntel", userAgent: "test" },
    MutationObserver: class { observe() {} disconnect() {} },
    define(_dependencies, factory) { initializer = factory(events, connectionManager); },
    setTimeout,
    clearTimeout,
    console
};

function evaluateAndStart() {
    vm.runInNewContext(source, sandbox, { filename: "external-player.js" });
    initializer();
}

evaluateAndStart();
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(document.body.walk().filter((item) => item.id === "embyExternalPlayerButton").length, 1);
assert.equal(eventSubscriptions.size, 1);

evaluateAndStart();
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(document.body.walk().filter((item) => item.id === "embyExternalPlayerButton").length, 1);
assert.equal(eventSubscriptions.size, 1, "reloading must unsubscribe the prior connection event");

const button = document.getElementById("embyExternalPlayerButton");
button.dispatch("click");
assert.ok(document.querySelector(".emby-external-player-overlay"));
document.dispatch("keydown", { key: "Escape", preventDefault() {} });
assert.equal(document.querySelector(".emby-external-player-overlay"), null);

console.log("Web module tests passed.");
