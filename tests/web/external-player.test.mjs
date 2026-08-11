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
        if (selector.includes("btnResume")) {
            return this.walk().find((item) => item.className.split(/\s+/).includes("btnResume")) || null;
        }
        if (selector.includes("btnPlay")) {
            return this.walk().find((item) => item.className.split(/\s+/).includes("btnPlay")) || null;
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

    get nextSibling() {
        if (!this.parentNode) return null;
        const index = this.parentNode.children.indexOf(this);
        return this.parentNode.children[index + 1] || null;
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
        this.playButton = new FakeElement("button", this);
        this.playButton.className = "raised emby-button detailButton btnPlay btnMainPlay";
        this.playButton.textContent = "从头开始";
        this.actionRow.appendChild(this.playButton);
        this.body.appendChild(this.actionRow);
    }

    createElement(tagName) {
        return new FakeElement(tagName, this);
    }

    createElementNS(_namespace, tagName) {
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
            return this.body.walk().find((item) => item.className.split(/\s+/).includes("emby-external-player-overlay")) || null;
        }
        return null;
    }

    querySelectorAll(selector) {
        if (selector.includes('data-data1="PageSave"') || selector.includes(".btnSave.pagebutton")) {
            return this.body.walk().filter((item) =>
                item.attributes.get("data-data1") === "PageSave" ||
                (item.className.split(/\s+/).includes("btnSave") &&
                 item.className.split(/\s+/).includes("pagebutton")));
        }
        return [];
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
    Players: [
        { Id: "Iina", DisplayName: "IINA", IsCustom: false, LaunchSchemes: ["iina"] },
        { Id: "custom-1", DisplayName: "myPLAYER pro", IsCustom: true, LaunchSchemes: ["myplayer"] }
    ],
    Texts: {
        ExternalPlay: "外部播放",
        ChoosePlayer: "选择播放器",
        Open: "打开",
        BuiltInPlayer: "内置",
        CustomPlayer: "自定义播放器",
        CustomPlayerHint: "在插件中配置的自定义应用会显示在此处。",
        NoCustomPlayerHint: "可在“外部播放器”插件设置中添加自定义应用。",
        MediaVersion: "媒体版本",
        VersionNumber: "版本 {0}",
        Subtitle: "字幕",
        NoExternalSubtitle: "不加载外挂字幕",
        SubtitleNumber: "字幕 {0}",
        ResumeFromLastPosition: "从上次位置继续",
        RetryLaunch: "若播放器未自动打开，请点此重试",
        Cancel: "取消",
        ResolveError: "无法生成播放地址，请检查权限、媒体版本或服务器连接。",
        InvalidLaunchUrl: "服务器未返回安全的应用启动地址。"
    }
};

const source = fs.readFileSync(
    new URL("../../src/Emby.ExternalPlayer/Resources/external-player.js", import.meta.url),
    "utf8");
const document = new FakeDocument();
const resumeButton = document.createElement("button");
resumeButton.className = "raised emby-button detailButton detailButton-primary detailButton-stacked btnResume";
resumeButton.textContent = "继续播放";
document.actionRow.insertBefore(resumeButton, document.playButton);
const eventSubscriptions = new Set();
let ajaxResponse = { LaunchUrl: "iina://weblink?url=https%3A%2F%2Fexample.test" };
let manifestQuery;
let lastResolveBody;
const events = {
    on(_source, name, handler) { eventSubscriptions.add(`${name}:${String(handler)}`); },
    off(_source, name, handler) { eventSubscriptions.delete(`${name}:${String(handler)}`); }
};
const apiClient = {
    getUrl(path, query) {
        if (path === "ExternalPlayer/Manifest") manifestQuery = query;
        return `http://127.0.0.1:18095/${path}`;
    },
    getJSON() { return Promise.resolve(manifest); },
    ajax(options) {
        assert.equal(options.dataType, "json", "Emby ajax must parse the Resolve response as JSON");
        lastResolveBody = JSON.parse(options.data);
        assert.equal(lastResolveBody.language, "zh-CN");
        return Promise.resolve(ajaxResponse);
    }
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
    navigator: { platform: "MacIntel", userAgent: "test", language: "zh-CN" },
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
assert.equal(manifestQuery.language, "zh-CN");
assert.equal(document.getElementById("embyExternalPlayerStyles").attributes.get("data-resource-version"), "1.3.1");

evaluateAndStart();
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(document.body.walk().filter((item) => item.id === "embyExternalPlayerButton").length, 1);
assert.equal(eventSubscriptions.size, 1, "reloading must unsubscribe the prior connection event");

const button = document.getElementById("embyExternalPlayerButton");
assert.ok(button.className.includes("raised"), "the detail action must use Emby's themed raised-button style");
assert.ok(!button.className.includes("detailButton-primary"), "the detail action must inherit the From Beginning action instead of Resume");
assert.ok(!button.className.includes("detailButton-autotext"));
assert.equal(
    document.actionRow.children.indexOf(button),
    document.actionRow.children.indexOf(document.playButton) + 1,
    "the external-play action must be placed immediately after From Beginning");
assert.ok(button.walk().some((item) => item.tagName === "SVG"), "the detail action must use an inline SVG icon");
assert.ok(button.walk().some((item) => item.textContent === "外部播放"), "the detail action must retain its visible label");
assert.ok(!button.walk().some((item) => item.textContent === "open_in_new"), "icon ligature text must never be visible");
button.dispatch("click");
const launchOverlay = document.querySelector(".emby-external-player-overlay");
assert.ok(launchOverlay);
assert.ok(!launchOverlay.walk().some((item) => item.className.includes("emby-external-player-close")), "the dialog must not duplicate Cancel with a large close icon");
const iinaOption = launchOverlay.walk().find((item) => item.attributes.get("data-player-id") === "Iina");
const customOption = launchOverlay.walk().find((item) => item.attributes.get("data-player-id") === "custom-1");
const launchButton = launchOverlay.walk().find((item) => item.tagName === "BUTTON" && item.textContent === "打开");
assert.ok(iinaOption);
assert.ok(customOption, "enabled custom applications must be visible in the chooser");
assert.equal(iinaOption.attributes.get("aria-checked"), "true");
assert.ok(iinaOption.className.includes("emby-external-player-option-selected"), "the default player must have a persistent selected state");
assert.notEqual(document.activeElement, iinaOption, "opening the chooser must not apply Emby's white focus style to IINA");
assert.equal(document.activeElement.attributes.get("role"), "dialog");
assert.ok(customOption.walk().some((item) => item.textContent === "自定义播放器"));
customOption.dispatch("click");
assert.equal(customOption.attributes.get("aria-checked"), "true");
iinaOption.dispatch("click");
launchButton.dispatch("click");
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(lastResolveBody.playerId, "Iina");
assert.equal(window.location.href, "iina://weblink?url=https%3A%2F%2Fexample.test");
document.dispatch("keydown", { key: "Escape", preventDefault() {} });
assert.equal(document.querySelector(".emby-external-player-overlay"), null);

window.location.href = "";
ajaxResponse = { LaunchUrl: "javascript:alert(1)" };
button.dispatch("click");
const unsafeOverlay = document.querySelector(".emby-external-player-overlay");
const unsafeLaunchButton = unsafeOverlay.walk().find((item) => item.tagName === "BUTTON" && item.textContent === "打开");
unsafeLaunchButton.dispatch("click");
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(window.location.href, "", "a scheme not declared by the selected player must be rejected");
document.dispatch("keydown", { key: "Escape", preventDefault() {} });

window.location.href = "";
ajaxResponse = {};
button.dispatch("click");
const invalidOverlay = document.querySelector(".emby-external-player-overlay");
const invalidLaunchButton = invalidOverlay.walk().find((item) => item.tagName === "BUTTON" && item.textContent === "打开");
invalidLaunchButton.dispatch("click");
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(window.location.href, "", "an invalid Resolve response must never navigate to /undefined");

const saveButton = document.createElement("button");
saveButton.className = "raised emby-button btnSave pagebutton";
saveButton.setAttribute("data-data1", "PageSave");
saveButton.textContent = "save";
document.body.appendChild(saveButton);
window.location.hash = "#!/genericui?PageId=f7e75c%3ASettings";
evaluateAndStart();
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(saveButton.textContent, "保存", "the plugin configuration Save command must follow the Emby client language");
assert.equal(saveButton.attributes.get("aria-label"), "保存");
assert.match(
    invalidOverlay.walk().find((item) => item.className === "emby-external-player-error").textContent,
    /无法生成播放地址/);
document.dispatch("keydown", { key: "Escape", preventDefault() {} });

console.log("Web module tests passed.");
