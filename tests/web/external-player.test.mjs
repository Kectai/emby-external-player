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
        if (child.parentNode) {
            child.parentNode.removeChild(child);
        }
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

    getAttribute(name) {
        return this.attributes.get(name) ?? null;
    }

    removeAttribute(name) {
        this.attributes.delete(name);
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

    click() {
        this.dispatch("click");
    }

    focus() {
        this.ownerDocument.activeElement = this;
    }

    getClientRects() {
        let current = this;
        while (current) {
            if (current.hidden || current.attributes.get("aria-hidden") === "true" ||
                current.className.split(/\s+/).includes("hide")) {
                return [];
            }
            current = current.parentNode;
        }
        return [{}];
    }

    querySelector(selector) {
        const attributeMatch = selector.match(/^\[([^=]+)="([^"]+)"\]$/);
        if (attributeMatch) {
            return this.walk().find((item) =>
                item.attributes.get(attributeMatch[1]) === attributeMatch[2]) || null;
        }
        if (selector.includes("btnResume")) {
            return this.walk().find((item) => item.className.split(/\s+/).includes("btnResume")) || null;
        }
        if (selector.includes("btnPlay")) {
            return this.walk().find((item) => item.className.split(/\s+/).includes("btnPlay")) || null;
        }
        if (selector === "button") {
            return this.walk().find((item) => item.tagName === "BUTTON") || null;
        }
        if (selector === '.emby-ep-select-button[aria-expanded="true"]') {
            return this.walk().find((item) =>
                item.className.split(/\s+/).includes("emby-ep-select-button") &&
                item.attributes.get("aria-expanded") === "true") || null;
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
        return this._textContent + this.children.map((child) => child.textContent).join("");
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
            return this.querySelectorAll(selector)[0] || null;
        }
        if (selector === ".mainContent") {
            return this.body.walk().find((item) => item.className.split(/\s+/).includes("mainContent")) || null;
        }
        if (selector === ".emby-ep-overlay") {
            return this.body.walk().find((item) => item.className.split(/\s+/).includes("emby-ep-overlay")) || null;
        }
        return null;
    }

    querySelectorAll(selector) {
        if (selector === ".mainContent") {
            return this.body.walk().filter((item) =>
                item.className.split(/\s+/).includes("mainContent"));
        }
        if (selector === ".emby-ep-native-save-hidden") {
            return this.body.walk().filter((item) =>
                item.className.split(/\s+/).includes("emby-ep-native-save-hidden"));
        }
        if (selector === "select.selectSource" || selector === "select.selectSubtitles") {
            const className = selector.split(".")[1];
            return this.body.walk().filter((item) =>
                item.tagName === "SELECT" && item.className.split(/\s+/).includes(className));
        }
        if (selector.startsWith(".mainDetailButtons")) {
            return this.body.walk().filter((item) =>
                item.className.split(/\s+/).includes("mainDetailButtons") ||
                item.className.split(/\s+/).includes("detailButtons"));
        }
        if (selector === "#embyExternalPlayerButton") {
            return this.body.walk().filter((item) => item.id === "embyExternalPlayerButton");
        }
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
    DefaultPlayerId: "Iina",
    MediaSources: [
        {
            Id: "source-1",
            Name: "4K REMUX",
            IsDefault: true,
            Subtitles: [{ Index: 3, DisplayTitle: "简体中文 ASS", IsDefault: true }]
        },
        {
            Id: "source-2",
            Name: "1080p WEB-DL",
            IsDefault: false,
            Subtitles: [{ Index: 6, DisplayTitle: "English SRT", IsDefault: false }]
        }
    ],
    Players: [
        { Id: "Iina", DisplayName: "IINA", IsCustom: false, SupportsExternalSubtitle: false, LaunchSchemes: ["iina"] },
        { Id: "custom-0123456789abcdef0123456789abcdef", DisplayName: "myPLAYER pro", IsCustom: true, SupportsExternalSubtitle: true, LaunchSchemes: ["myplayer"] }
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
        SubtitleMayNotLoadForPlayer: "{0} 的应用跳转可能不会自动加载所选外挂字幕，但不会影响媒体打开。",
        PlaybackPreferences: "播放偏好",
        DefaultPlayer: "默认播放器",
        DefaultPlayerSaved: "已保存默认播放器。",
        DefaultPlayerSaveError: "无法保存默认播放器。",
        ResumeFromLastPosition: "从上次位置继续",
        Cancel: "取消",
        ResolveError: "无法生成播放地址，请检查权限、媒体版本或服务器连接。",
        InvalidLaunchUrl: "服务器未返回安全的应用启动地址。"
    }
};

const source = fs.readFileSync(
    new URL("../../src/Emby.ExternalPlayer/Resources/external-player.js", import.meta.url),
    "utf8");
const stylesheet = fs.readFileSync(
    new URL("../../src/Emby.ExternalPlayer/Resources/external-player.css", import.meta.url),
    "utf8");
assert.match(stylesheet, /\.dialog\.formDialog\.emby-ep-dialog\s*\{[\s\S]*?max-width:\s*44rem\s*!important;/);
assert.match(stylesheet, /\.dialog\.formDialog\.emby-ep-dialog\s*\{[\s\S]*?min-width:\s*0\s*!important;/);
assert.match(stylesheet, /width:\s*min\(clamp\(36rem,\s*52vw,\s*44rem\),\s*calc\(100vw\s*-\s*3rem\)\)\s*!important;/);
assert.match(stylesheet, /\.emby-ep-actions \.formDialogFooterItem\s*\{[\s\S]*?justify-content:\s*center\s*!important;/);
assert.match(stylesheet, /\.emby-ep-fields \.selectContainer\.emby-ep-field\s*\{[\s\S]*?margin:\s*0\s*!important;/);
assert.match(stylesheet, /\.emby-ep-select-button\s*\{[\s\S]*?height:\s*3em\s*!important;/);
assert.match(stylesheet, /\.emby-ep-select-list\s*\{[\s\S]*?left:\s*0;[\s\S]*?right:\s*0;[\s\S]*?width:\s*100%;/);
assert.match(stylesheet, /\.emby-ep-native-select\s*\{[\s\S]*?display:\s*none\s*!important;/);
assert.match(stylesheet, /\.emby-ep-fields\s*\{[\s\S]*?padding:\s*1\.4rem \.15rem 0;/);
assert.match(stylesheet, /\.emby-ep-player-list\s*\{[\s\S]*?grid-template-columns:\s*repeat\(2,minmax\(0,1fr\)\);/);
assert.match(stylesheet, /\.emby-ep-fields\s*\{[\s\S]*?grid-template-columns:\s*repeat\(2,minmax\(0,1fr\)\);/);
assert.match(stylesheet, /\.emby-ep-config-section\s*\{[\s\S]*?width:\s*100%;/);
assert.match(stylesheet, /\.emby-ep-config-fields\s*\{[\s\S]*?grid-template-columns:\s*minmax\(0,\s*1fr\);/);
assert.match(stylesheet, /\.emby-ep-config-fields\s*\{[\s\S]*?gap:\s*\.75rem 1rem;/);
assert.match(stylesheet, /\.emby-ep-config-platform-field\s*\{[\s\S]*?grid-column:\s*1 \/ -1;/);
assert.match(stylesheet, /\.emby-ep-config-platforms\s*\{[\s\S]*?flex-wrap:\s*wrap;/);
assert.match(stylesheet, /\.emby-ep-config-platform-option-selected\s*\{/);
assert.match(stylesheet, /\.emby-ep-config-builtin-row\s*\{[\s\S]*?grid-template-columns:\s*minmax\(8rem,11rem\) minmax\(0,1fr\);/);
assert.match(stylesheet, /\.emby-ep-config-builtin-row \.emby-ep-config-card-status\s*\{[\s\S]*?grid-column:\s*2;[\s\S]*?overflow-wrap:\s*anywhere;/);
assert.match(stylesheet, /\.emby-ep-config-builtin-row \.emby-ep-config-platform-option\s*\{[\s\S]*?font-size:\s*\.9rem;/);
assert.match(stylesheet, /\.emby-ep-config-card-actions \.emby-button\s*\{[\s\S]*?justify-content:\s*center\s*!important;/);
assert.match(stylesheet, /\.emby-ep-config-card-status\[data-state="dirty"\]\s*\{/);
assert.match(stylesheet, /\.emby-ep-config-input,[\s\S]*?\.emby-ep-config-select\s*\{[\s\S]*?height:\s*3em\s*!important;/);
assert.match(stylesheet, /\.emby-ep-toggle-switch\s*\{[\s\S]*?width:\s*3em;/);
assert.match(stylesheet, /\.emby-ep-toggle-input:checked\s*~\s*\.emby-ep-toggle-switch\s*\{[\s\S]*?var\(--ep-accent\)/);
assert.match(stylesheet, /\.emby-ep-default-field\s*\{/);
assert.match(
    stylesheet,
    /@media not all and \(min-width:50em\)\{\.detailButtons \.detailButton\.emby-ep-button\{[\s\S]*?flex-basis:100% !important;[\s\S]*?flex-grow:1 !important;[\s\S]*?flex-shrink:0 !important;[\s\S]*?max-width:100%;\}\}/,
    "the external-play action must keep a full mobile row even when Emby marks the play button as stacked");
const document = new FakeDocument();
const resumeButton = document.createElement("button");
resumeButton.className = "raised emby-button detailButton detailButton-primary detailButton-stacked btnResume";
resumeButton.textContent = "继续播放";
document.actionRow.insertBefore(resumeButton, document.playButton);
const eventSubscriptions = new Set();
let ajaxResponse = { LaunchUrl: "iina://weblink?url=https%3A%2F%2Fexample.test" };
let manifestQuery;
let lastResolveBody;
let lastCustomPlayerPostBody;
let lastBuiltInPlatformsPostBody;
let rejectBuiltInPlatforms = false;
const defaultPreferenceBodies = [];
let rejectDefaultPreference = false;
let deferDefaultPreferences = false;
const pendingDefaultPreferences = [];
let deferNativeConfigurationSaves = false;
let rejectNativeConfigurationSave = false;
const pendingNativeConfigurationSaves = [];
let currentUserId = "user-a";
let currentServerId = "server-a";
const events = {
    on(_source, name, handler) { eventSubscriptions.add(`${name}:${String(handler)}`); },
    off(_source, name, handler) { eventSubscriptions.delete(`${name}:${String(handler)}`); }
};
const apiClient = {
    getCurrentUserId() { return currentUserId; },
    serverId() { return currentServerId; },
    getUrl(path, query) {
        if (path === "ExternalPlayer/Manifest") manifestQuery = query;
        return `http://127.0.0.1:18095/${path}`;
    },
    getJSON(url) {
        if (String(url).includes("ExternalPlayer/BuiltInPlayerPlatforms")) {
            return Promise.resolve([
                { PlayerId: "PotPlayer", DisplayName: "PotPlayer", Platforms: ["Windows"] },
                { PlayerId: "Iina", DisplayName: "IINA", Platforms: ["MacOS"] },
                { PlayerId: "Vlc", DisplayName: "VLC media player", Platforms: ["Windows", "MacOS", "IOS", "Android", "Linux"] },
                { PlayerId: "Infuse", DisplayName: "Infuse", Platforms: ["MacOS", "IOS"] },
                { PlayerId: "Mpv", DisplayName: "mpv", Platforms: ["Windows", "MacOS", "Linux"] },
                { PlayerId: "NPlayer", DisplayName: "nPlayer", Platforms: ["IOS", "Android"] }
            ]);
        }
        if (String(url).includes("ExternalPlayer/CustomPlayers")) {
            return Promise.resolve([{
                Id: "custom-config-1",
                Enabled: true,
                ApplicationName: "IINA Nova",
                Platform: "MacOS",
                Platforms: ["MacOS", "IOS"],
                UrlTemplate: "iina-nova://weblink?url={url}&new_window=1&mpv_start={start}&mpv_http-header-fields={headers}",
                EnablePlaybackReporting: true
            }]);
        }
        return Promise.resolve(manifest);
    },
    ajax(options) {
        if (String(options.url).includes("UI/Command")) {
            if (rejectNativeConfigurationSave) {
                return Promise.reject(new Error("native configuration save failed"));
            }
            if (deferNativeConfigurationSaves) {
                return new Promise((resolve, reject) => {
                    pendingNativeConfigurationSaves.push({ resolve, reject });
                });
            }
            return Promise.resolve({});
        }
        if (String(options.url).includes("ExternalPlayer/BuiltInPlayerPlatforms")) {
            const value = JSON.parse(options.data);
            lastBuiltInPlatformsPostBody = value;
            if (rejectBuiltInPlatforms) return Promise.reject(new Error("platform save failed"));
            return Promise.resolve({
                PlayerId: value.playerId,
                DisplayName: value.playerId,
                Platforms: value.platforms
            });
        }
        if (String(options.url).includes("ExternalPlayer/UserDefaultPlayer")) {
            const value = JSON.parse(options.data);
            defaultPreferenceBodies.push(value);
            if (rejectDefaultPreference) return Promise.reject(new Error("preference save failed"));
            if (deferDefaultPreferences) {
                return new Promise((resolve, reject) => {
                    pendingDefaultPreferences.push({ value, resolve, reject });
                });
            }
            return Promise.resolve({ Platform: value.platform, PlayerId: value.playerId });
        }
        if (String(options.url).includes("ExternalPlayer/CustomPlayers")) {
            const value = JSON.parse(options.data);
            lastCustomPlayerPostBody = value;
            return Promise.resolve({ ...value, Id: value.id || "saved-custom-player" });
        }
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
    clearTimeout,
    getComputedStyle(element) {
        return {
            display: element.hidden ? "none" : "block",
            visibility: "visible"
        };
    }
};
const mutationObservers = [];
class FakeMutationObserver {
    constructor(callback) {
        this.callback = callback;
        this.connected = false;
        mutationObservers.push(this);
    }

    observe() { this.connected = true; }
    disconnect() { this.connected = false; }
    trigger() { if (this.connected) this.callback([]); }
}
let initializer;
const navigator = { platform: "MacIntel", userAgent: "test", language: "zh-CN", maxTouchPoints: 0 };
const sandbox = {
    window,
    document,
    navigator,
    MutationObserver: FakeMutationObserver,
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
assert.match(document.getElementById("embyExternalPlayerStyles").href, /ExternalPlayer\/Web\/style\.css$/);

evaluateAndStart();
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(document.body.walk().filter((item) => item.id === "embyExternalPlayerButton").length, 1);
assert.equal(eventSubscriptions.size, 1, "reloading must unsubscribe the prior connection event");

let button = document.getElementById("embyExternalPlayerButton");
assert.ok(button.className.includes("raised"), "the detail action must use Emby's themed raised-button style");
assert.ok(!button.className.includes("detailButton-primary"), "the detail action must inherit the From Beginning action instead of Resume");
assert.ok(!button.className.includes("detailButton-autotext"));
assert.equal(
    document.actionRow.children.indexOf(button),
    document.actionRow.children.indexOf(document.playButton) + 1,
    "the external-play action must be placed immediately after From Beginning");
assert.ok(button.walk().some((item) => item.tagName === "I" && item.textContent === "\ue89e"), "the detail action must use Emby's native icon structure without a ligature word");
assert.ok(button.walk().some((item) => item.textContent === "外部播放"), "the detail action must retain its visible label");
assert.ok(!button.walk().some((item) => item.textContent === "open_in_new"), "icon ligature text must never be visible");

button.remove();
mutationObservers.at(-1).trigger();
button = document.getElementById("embyExternalPlayerButton");
assert.ok(button, "the persistent item-page observer must restore the button after Emby rebuilds the view");
assert.equal(
    document.actionRow.children.indexOf(button),
    document.actionRow.children.indexOf(document.playButton) + 1,
    "the restored action must remain after From Beginning");

document.actionRow.hidden = true;
const secondVisitActionRow = document.createElement("div");
secondVisitActionRow.className = "mainDetailButtons";
const secondVisitPlayButton = document.createElement("button");
secondVisitPlayButton.className = "raised emby-button detailButton btnPlay btnMainPlay";
secondVisitPlayButton.textContent = "从头开始";
secondVisitActionRow.appendChild(secondVisitPlayButton);
document.body.appendChild(secondVisitActionRow);
document.dispatch("viewshow", {});
await new Promise((resolve) => setTimeout(resolve, 0));
button = document.getElementById("embyExternalPlayerButton");
assert.equal(button.parentNode, secondVisitActionRow, "the second visit must target the visible detail view instead of Emby's retained hidden view");
assert.equal(
    secondVisitActionRow.children.indexOf(button),
    secondVisitActionRow.children.indexOf(secondVisitPlayButton) + 1,
    "the second-visit action must remain after From Beginning");
assert.equal(document.querySelectorAll("#embyExternalPlayerButton").length, 1);

const originalGetJson = apiClient.getJSON;
const pendingManifestRequests = [];
apiClient.getJSON = function (url) {
    if (String(url).includes("ExternalPlayer/Manifest")) {
        return new Promise((resolve, reject) => pendingManifestRequests.push({ resolve, reject }));
    }
    return originalGetJson.call(apiClient, url);
};
window.location.hash = "#!/item?id=old-request";
document.dispatch("viewshow", {});
await new Promise((resolve) => setTimeout(resolve, 0));
window.location.hash = "#!/item?id=current-request";
document.dispatch("viewshow", {});
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(pendingManifestRequests.length, 2);
pendingManifestRequests[1].resolve({ ...manifest, ItemId: "current-request" });
await new Promise((resolve) => setTimeout(resolve, 0));
assert.ok(document.getElementById("embyExternalPlayerButton"));
pendingManifestRequests[0].reject(new Error("stale request failed late"));
await new Promise((resolve) => setTimeout(resolve, 0));
assert.ok(
    document.getElementById("embyExternalPlayerButton"),
    "a stale Manifest rejection must not remove the current page's button");
apiClient.getJSON = originalGetJson;

navigator.maxTouchPoints = 5;
document.dispatch("viewshow", {});
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(manifestQuery.platform, "IOS", "desktop-mode iPadOS must not be filtered as macOS");
navigator.maxTouchPoints = 0;
document.dispatch("viewshow", {});
await new Promise((resolve) => setTimeout(resolve, 0));

const oldSourceSelect = document.createElement("select");
oldSourceSelect.className = "selectSource";
oldSourceSelect.value = "source-2";
oldSourceSelect.hidden = true;
document.body.appendChild(oldSourceSelect);
const currentSourceSelect = document.createElement("select");
currentSourceSelect.className = "selectSource";
currentSourceSelect.value = "source-1";
document.body.appendChild(currentSourceSelect);
const oldSubtitleSelect = document.createElement("select");
oldSubtitleSelect.className = "selectSubtitles";
oldSubtitleSelect.value = "3";
oldSubtitleSelect.hidden = true;
document.body.appendChild(oldSubtitleSelect);
const currentSubtitleSelect = document.createElement("select");
currentSubtitleSelect.className = "selectSubtitles";
currentSubtitleSelect.value = "6";
document.body.appendChild(currentSubtitleSelect);

button = document.getElementById("embyExternalPlayerButton");
button.dispatch("click");
const launchOverlay = document.querySelector(".emby-ep-overlay");
assert.ok(launchOverlay);
assert.ok(!launchOverlay.walk().some((item) => item.className.includes("emby-ep-close")), "the dialog must not duplicate Cancel with a large close icon");
const iinaOption = launchOverlay.walk().find((item) => item.attributes.get("data-player-id") === "Iina");
const customOption = launchOverlay.walk().find((item) => item.attributes.get("data-player-id") === "custom-0123456789abcdef0123456789abcdef");
const launchButton = launchOverlay.walk().find((item) => item.tagName === "BUTTON" && item.textContent === "打开");
const nativeSelectors = launchOverlay.walk().filter((item) =>
    item.className.split(/\s+/).includes("emby-ep-native-select"));
const selectorButtons = launchOverlay.walk().filter((item) =>
    item.className.split(/\s+/).includes("emby-ep-select-button"));
const selectorLists = launchOverlay.walk().filter((item) =>
    item.className.split(/\s+/).includes("emby-ep-select-list"));
const subtitleHint = launchOverlay.walk().find((item) =>
    item.className.split(/\s+/).includes("emby-ep-subtitle-hint"));
const defaultPlayerSelect = nativeSelectors.find((item) =>
    item.parentNode?.className.split(/\s+/).includes("emby-ep-default-field"));
const defaultPlayerStatus = launchOverlay.walk().find((item) =>
    item.className.split(/\s+/).includes("emby-ep-default-status"));
const preferenceSection = launchOverlay.walk().find((item) =>
    item.className.split(/\s+/).includes("emby-ep-preferences"));
const playbackFields = launchOverlay.walk().find((item) =>
    item.className.split(/\s+/).includes("emby-ep-fields"));
assert.ok(iinaOption);
assert.ok(customOption, "enabled custom applications must be visible in the chooser");
assert.ok(defaultPlayerSelect, "authenticated users must be able to choose a personal platform default");
assert.equal(defaultPlayerSelect.value, "Iina", "the selector must show the effective default player");
assert.ok(preferenceSection.walk().some((item) => item.textContent === "播放偏好"));
assert.ok(preferenceSection.parentNode.children.indexOf(preferenceSection) >
    preferenceSection.parentNode.children.indexOf(playbackFields),
"the persistent preference must stay below the one-time playback controls");
assert.ok(!launchOverlay.walk().some((item) =>
    item.className.split(/\s+/).includes("emby-ep-manual-link")),
"the dialog must not duplicate Open with a retry link");
assert.equal(iinaOption.attributes.get("aria-checked"), "true");
assert.equal(iinaOption.attributes.get("tabindex"), "0");
assert.equal(customOption.attributes.get("tabindex"), "-1");
assert.ok(iinaOption.className.includes("emby-ep-option-selected"), "the default player must have a persistent selected state");
assert.notEqual(document.activeElement, iinaOption, "opening the chooser must not apply Emby's white focus style to IINA");
assert.equal(document.activeElement.attributes.get("role"), "dialog");
assert.equal(nativeSelectors.length, 3);
assert.ok(nativeSelectors.every((item) => item.hidden), "native selects must stay out of the visible and accessibility layout");
assert.equal(selectorButtons.length, 3);
assert.equal(selectorLists.length, 3);
assert.match(selectorButtons[2].textContent, /IINA/);
assert.ok(!selectorLists[2].walk().some((item) => item.textContent.includes("管理员")));
assert.equal(nativeSelectors[0].value, "source-1", "the chooser must read the visible media-source selector");
assert.equal(selectorButtons[1].disabled, false, "an unverified application link must not block subtitle selection");
assert.match(subtitleHint.textContent, /IINA/);
assert.equal(subtitleHint.hidden, false);
selectorButtons[0].dispatch("click");
assert.equal(selectorButtons[0].attributes.get("aria-expanded"), "true");
assert.equal(selectorLists[0].hidden, false);
const firstSourceOption = selectorLists[0].walk().find((item) => item.attributes.get("data-value") === "source-1");
let tabPrevented = false;
firstSourceOption.dispatch("keydown", { key: "Tab", preventDefault() { tabPrevented = true; } });
assert.equal(tabPrevented, true, "Tab inside a custom listbox must be handled by the dialog");
assert.equal(document.activeElement, selectorButtons[0], "closing a listbox with Tab must keep focus inside the modal");
selectorButtons[0].dispatch("click");
document.dispatch("keydown", { key: "Escape", preventDefault() {} });
assert.equal(selectorButtons[0].attributes.get("aria-expanded"), "false", "Escape must close the selector before the dialog");
assert.equal(document.querySelector(".emby-ep-overlay"), launchOverlay);
selectorButtons[0].dispatch("click");
const secondSourceOption = selectorLists[0].walk().find((item) => item.attributes.get("data-value") === "source-2");
secondSourceOption.dispatch("click");
assert.equal(nativeSelectors[0].value, "source-2");
assert.match(selectorButtons[0].textContent, /1080p WEB-DL/);
assert.match(selectorButtons[1].textContent, /English SRT/);
assert.equal(subtitleHint.hidden, false, "an unverified handler shows a reminder without clearing the page subtitle");
assert.ok(customOption.walk().some((item) => item.textContent === "自定义播放器"));
customOption.dispatch("click");
assert.equal(customOption.attributes.get("aria-checked"), "true");
assert.equal(customOption.attributes.get("tabindex"), "0");
assert.equal(iinaOption.attributes.get("tabindex"), "-1");
assert.equal(selectorButtons[1].disabled, false);
assert.equal(subtitleHint.hidden, true);
defaultPlayerSelect.value = "custom-0123456789abcdef0123456789abcdef";
defaultPlayerSelect.dispatch("change");
await new Promise((resolve) => setTimeout(resolve, 0));
assert.deepEqual(defaultPreferenceBodies.at(-1), {
    platform: "MacOS",
    playerId: "custom-0123456789abcdef0123456789abcdef"
});
assert.equal(defaultPlayerSelect.value, "custom-0123456789abcdef0123456789abcdef");
assert.equal(manifest.DefaultPlayerId, "custom-0123456789abcdef0123456789abcdef");
assert.equal(defaultPlayerStatus.textContent, "已保存默认播放器。");
rejectDefaultPreference = true;
defaultPlayerSelect.value = "Iina";
defaultPlayerSelect.dispatch("change");
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(defaultPlayerSelect.value, "custom-0123456789abcdef0123456789abcdef",
    "a failed preference save must restore the last persisted value");
assert.equal(manifest.DefaultPlayerId, "custom-0123456789abcdef0123456789abcdef");
assert.equal(defaultPlayerStatus.textContent, "无法保存默认播放器。");
rejectDefaultPreference = false;
nativeSelectors[0].dispatch("change");
assert.equal(nativeSelectors[1].value, "6", "the chooser must read the visible subtitle selector");
selectorButtons[1].dispatch("click");
const secondSubtitleOption = selectorLists[1].walk().find((item) => item.attributes.get("data-value") === "6");
secondSubtitleOption.dispatch("click");
assert.equal(nativeSelectors[1].value, "6");
ajaxResponse = { LaunchUrl: "myplayer://open?url=https%3A%2F%2Fexample.test" };
launchButton.dispatch("click");
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(lastResolveBody.playerId, "custom-0123456789abcdef0123456789abcdef");
assert.equal(lastResolveBody.subtitleStreamIndex, 6, "a supported player must receive the selected subtitle index");
assert.equal(window.location.href, "myplayer://open?url=https%3A%2F%2Fexample.test");
ajaxResponse = { LaunchUrl: "iina://weblink?url=https%3A%2F%2Fexample.test" };
iinaOption.dispatch("click");
assert.equal(defaultPlayerSelect.value, "custom-0123456789abcdef0123456789abcdef", "the launch choice must not silently change the saved default");
defaultPlayerSelect.dispatch("change");
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(iinaOption.attributes.get("aria-checked"), "true",
    "changing the persistent default must not replace the one-time playback choice");
defaultPlayerSelect.value = "Iina";
defaultPlayerSelect.dispatch("change");
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(defaultPreferenceBodies.at(-1).playerId, "Iina");
assert.equal(nativeSelectors[1].value, "6", "switching players must preserve the selected subtitle");
assert.equal(selectorButtons[1].disabled, false);
assert.equal(subtitleHint.hidden, false);
launchButton.dispatch("click");
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(lastResolveBody.playerId, "Iina");
assert.equal(lastResolveBody.subtitleStreamIndex, 6, "the server must accept a non-blocking subtitle preference");
assert.equal(window.location.href, "iina://weblink?url=https%3A%2F%2Fexample.test");
document.dispatch("keydown", { key: "Escape", preventDefault() {} });
assert.equal(document.querySelector(".emby-ep-overlay"), null);

button.dispatch("click");
const preferenceOverlay = document.querySelector(".emby-ep-overlay");
const preferenceSelect = preferenceOverlay.walk().find((item) =>
    item.className.split(/\s+/).includes("emby-ep-native-select") &&
    item.parentNode?.className.split(/\s+/).includes("emby-ep-default-field"));
preferenceSelect.value = "custom-0123456789abcdef0123456789abcdef";
preferenceSelect.dispatch("change");
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(defaultPreferenceBodies.at(-1).playerId,
    "custom-0123456789abcdef0123456789abcdef");
assert.equal(manifest.DefaultPlayerId, "custom-0123456789abcdef0123456789abcdef");
document.dispatch("keydown", { key: "Escape", preventDefault() {} });
button.dispatch("click");
const reopenedPreferenceOverlay = document.querySelector(".emby-ep-overlay");
const reopenedCustomOption = reopenedPreferenceOverlay.walk().find((item) =>
    item.attributes.get("data-player-id") === "custom-0123456789abcdef0123456789abcdef");
const reopenedPreferenceSelect = reopenedPreferenceOverlay.walk().find((item) =>
    item.className.split(/\s+/).includes("emby-ep-native-select") &&
    item.parentNode?.className.split(/\s+/).includes("emby-ep-default-field"));
assert.equal(reopenedCustomOption.attributes.get("aria-checked"), "true",
    "reopening the chooser on the same media page must select the saved personal default");
assert.equal(reopenedPreferenceSelect.value, "custom-0123456789abcdef0123456789abcdef");
document.dispatch("keydown", { key: "Escape", preventDefault() {} });

deferDefaultPreferences = true;
button.dispatch("click");
const firstPendingOverlay = document.querySelector(".emby-ep-overlay");
const firstPendingSelect = firstPendingOverlay.walk().find((item) =>
    item.className.split(/\s+/).includes("emby-ep-native-select") &&
    item.parentNode?.className.split(/\s+/).includes("emby-ep-default-field"));
firstPendingSelect.value = "Iina";
firstPendingSelect.dispatch("change");
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(pendingDefaultPreferences.length, 1);
document.dispatch("keydown", { key: "Escape", preventDefault() {} });
button.dispatch("click");
const secondPendingOverlay = document.querySelector(".emby-ep-overlay");
const secondPendingSelect = secondPendingOverlay.walk().find((item) =>
    item.className.split(/\s+/).includes("emby-ep-native-select") &&
    item.parentNode?.className.split(/\s+/).includes("emby-ep-default-field"));
secondPendingSelect.value = "custom-0123456789abcdef0123456789abcdef";
secondPendingSelect.dispatch("change");
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(pendingDefaultPreferences.length, 1,
    "a second dialog must queue its save behind the first request for the same user and platform");
pendingDefaultPreferences.shift().resolve({ Platform: "MacOS", PlayerId: "Iina" });
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(secondPendingSelect.value, "custom-0123456789abcdef0123456789abcdef",
    "an older completion must not overwrite the newest dialog selection");
assert.equal(pendingDefaultPreferences.length, 1, "the newest save starts only after the older save completes");
pendingDefaultPreferences.shift().resolve({
    Platform: "MacOS",
    PlayerId: "custom-0123456789abcdef0123456789abcdef"
});
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(manifest.DefaultPlayerId, "custom-0123456789abcdef0123456789abcdef");
document.dispatch("keydown", { key: "Escape", preventDefault() {} });
deferDefaultPreferences = false;

deferDefaultPreferences = true;
currentUserId = "user-a";
button.dispatch("click");
const switchingUserFirstOverlay = document.querySelector(".emby-ep-overlay");
const switchingUserFirstSelect = switchingUserFirstOverlay.walk().find((item) =>
    item.className.split(/\s+/).includes("emby-ep-native-select") &&
    item.parentNode?.className.split(/\s+/).includes("emby-ep-default-field"));
switchingUserFirstSelect.value = "Iina";
switchingUserFirstSelect.dispatch("change");
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(pendingDefaultPreferences.length, 1);
document.dispatch("keydown", { key: "Escape", preventDefault() {} });
button.dispatch("click");
const switchingUserQueuedOverlay = document.querySelector(".emby-ep-overlay");
const switchingUserQueuedSelect = switchingUserQueuedOverlay.walk().find((item) =>
    item.className.split(/\s+/).includes("emby-ep-native-select") &&
    item.parentNode?.className.split(/\s+/).includes("emby-ep-default-field"));
switchingUserQueuedSelect.value = "custom-0123456789abcdef0123456789abcdef";
switchingUserQueuedSelect.dispatch("change");
await new Promise((resolve) => setTimeout(resolve, 0));
document.dispatch("keydown", { key: "Escape", preventDefault() {} });
currentUserId = "user-b";
button.dispatch("click");
const switchedUserOverlay = document.querySelector(".emby-ep-overlay");
const switchedUserSelect = switchedUserOverlay.walk().find((item) =>
    item.className.split(/\s+/).includes("emby-ep-native-select") &&
    item.parentNode?.className.split(/\s+/).includes("emby-ep-default-field"));
const switchedUserInitialValue = switchedUserSelect.value;
pendingDefaultPreferences.shift().resolve({ Platform: "MacOS", PlayerId: "Iina" });
await new Promise((resolve) => setTimeout(resolve, 0));
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(switchedUserSelect.value, switchedUserInitialValue,
    "a completed save from the previous user must not update the current user's dialog");
assert.equal(pendingDefaultPreferences.length, 0,
    "a queued save must be cancelled before sending when the authenticated user changes");
const preferenceCountBeforeStaleDialogChange = defaultPreferenceBodies.length;
currentUserId = "user-a";
switchedUserSelect.value = "Iina";
switchedUserSelect.dispatch("change");
assert.equal(defaultPreferenceBodies.length, preferenceCountBeforeStaleDialogChange,
    "a dialog opened for another user must fail closed after the login changes");
assert.equal(switchedUserSelect.disabled, true);
document.dispatch("keydown", { key: "Escape", preventDefault() {} });
deferDefaultPreferences = false;
currentServerId = "server-b";
button.dispatch("click");
const switchedServerOverlay = document.querySelector(".emby-ep-overlay");
const switchedServerSelect = switchedServerOverlay.walk().find((item) =>
    item.className.split(/\s+/).includes("emby-ep-native-select") &&
    item.parentNode?.className.split(/\s+/).includes("emby-ep-default-field"));
const preferenceCountBeforeServerSwitch = defaultPreferenceBodies.length;
currentServerId = "server-a";
switchedServerSelect.value = "Iina";
switchedServerSelect.dispatch("change");
assert.equal(defaultPreferenceBodies.length, preferenceCountBeforeServerSwitch,
    "a dialog opened for another server must fail closed after the server changes");
assert.equal(switchedServerSelect.disabled, true);
document.dispatch("keydown", { key: "Escape", preventDefault() {} });
currentUserId = "";
button.dispatch("click");
const anonymousContextOverlay = document.querySelector(".emby-ep-overlay");
const anonymousContextSelect = anonymousContextOverlay.walk().find((item) =>
    item.className.split(/\s+/).includes("emby-ep-native-select") &&
    item.parentNode?.className.split(/\s+/).includes("emby-ep-default-field"));
assert.equal(anonymousContextSelect.disabled, true, "missing authenticated identity must disable preferences");
document.dispatch("keydown", { key: "Escape", preventDefault() {} });
currentUserId = "user-a";
const originalPlatform = navigator.platform;
navigator.platform = "Plan9";
button.dispatch("click");
const unknownPlatformOverlay = document.querySelector(".emby-ep-overlay");
const unknownPlatformSelect = unknownPlatformOverlay.walk().find((item) =>
    item.className.split(/\s+/).includes("emby-ep-native-select") &&
    item.parentNode?.className.split(/\s+/).includes("emby-ep-default-field"));
assert.equal(unknownPlatformSelect.disabled, true, "unknown platforms must fail closed");
document.dispatch("keydown", { key: "Escape", preventDefault() {} });
navigator.platform = originalPlatform;

window.location.href = "";
ajaxResponse = { LaunchUrl: "javascript:alert(1)" };
button.dispatch("click");
const unsafeOverlay = document.querySelector(".emby-ep-overlay");
const unsafeLaunchButton = unsafeOverlay.walk().find((item) => item.tagName === "BUTTON" && item.textContent === "打开");
unsafeLaunchButton.dispatch("click");
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(window.location.href, "", "a scheme not declared by the selected player must be rejected");
document.dispatch("keydown", { key: "Escape", preventDefault() {} });

window.location.href = "";
ajaxResponse = {};
button.dispatch("click");
const invalidOverlay = document.querySelector(".emby-ep-overlay");
const invalidLaunchButton = invalidOverlay.walk().find((item) => item.tagName === "BUTTON" && item.textContent === "打开");
invalidLaunchButton.dispatch("click");
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(window.location.href, "", "an invalid Resolve response must never navigate to /undefined");

const saveButton = document.createElement("button");
saveButton.className = "raised emby-button btnSave pagebutton";
saveButton.setAttribute("data-data1", "PageSave");
saveButton.textContent = "save";
document.body.appendChild(saveButton);
const configurationMain = document.createElement("div");
configurationMain.className = "mainContent";
document.body.appendChild(configurationMain);
window.location.hash = "#!/genericui?PageId=f7e75c%3ASettings";
evaluateAndStart();
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(
    document.getElementById("embyExternalPlayerConfigurationSave"),
    null,
    "the configuration page must not add a replacement Save command");
assert.ok(saveButton.className.includes("emby-ep-native-save-hidden"));
let nativeSaveClicks = 0;
saveButton.addEventListener("click", () => {
    nativeSaveClicks += 1;
    apiClient.ajax({
        type: "POST",
        url: apiClient.getUrl("UI/Command"),
        data: JSON.stringify({ PageId: "f7e75c:Settings", CommandId: "PageSave" })
    });
    configurationMain.dispatch("change", { target: configurationMain, isTrusted: false });
});
configurationMain.dispatch("change", { target: configurationMain, isTrusted: false });
configurationMain.dispatch("input", { target: configurationMain, isTrusted: false });
await new Promise((resolve) => setTimeout(resolve, 180));
assert.equal(nativeSaveClicks, 0,
    "script-generated Generic UI initialization events must never trigger automatic saving");
configurationMain.dispatch("click", { target: configurationMain, isTrusted: true });
configurationMain.dispatch("change", { target: configurationMain, isTrusted: false });
await new Promise((resolve) => setTimeout(resolve, 180));
assert.equal(nativeSaveClicks, 1,
    "a custom Emby control may dispatch a synthetic change during a real user interaction");
configurationMain.dispatch("change", { target: configurationMain, isTrusted: true });
await new Promise((resolve) => setTimeout(resolve, 180));
assert.equal(nativeSaveClicks, 2, "a basic option change must submit through Emby's native handler");
const ticketLifetimeInput = document.createElement("input");
ticketLifetimeInput.setAttribute("name", "TicketLifetimeMinutes");
ticketLifetimeInput.value = "360";
configurationMain.appendChild(ticketLifetimeInput);
configurationMain.dispatch("input", { target: ticketLifetimeInput, isTrusted: true });
configurationMain.dispatch("input", { target: ticketLifetimeInput, isTrusted: true });
await new Promise((resolve) => setTimeout(resolve, 750));
assert.equal(nativeSaveClicks, 3,
    "ticket lifetime input must be debounced into one automatic native save");
configurationMain.dispatch("change", { target: ticketLifetimeInput, isTrusted: true });
await new Promise((resolve) => setTimeout(resolve, 180));
assert.equal(nativeSaveClicks, 3,
    "ticket lifetime blur must not resubmit the value just saved from input");
ticketLifetimeInput.value = "361";
configurationMain.dispatch("change", { target: ticketLifetimeInput, isTrusted: true });
await new Promise((resolve) => setTimeout(resolve, 180));
assert.equal(nativeSaveClicks, 4, "a genuinely changed ticket lifetime must still save");
saveButton.hidden = true;
const rebuiltSaveButton = document.createElement("button");
rebuiltSaveButton.className = "raised emby-button btnSave pagebutton";
rebuiltSaveButton.setAttribute("data-data1", "PageSave");
rebuiltSaveButton.textContent = "Save";
let rebuiltNativeSaveClicks = 0;
rebuiltSaveButton.addEventListener("click", () => {
    rebuiltNativeSaveClicks += 1;
    apiClient.ajax({
        type: "POST",
        url: apiClient.getUrl("UI/Command"),
        data: JSON.stringify({ PageId: "f7e75c:Settings", CommandId: "PageSave" })
    });
});
document.body.appendChild(rebuiltSaveButton);
mutationObservers.at(-1).trigger();
configurationMain.dispatch("change", { target: configurationMain, isTrusted: true });
await new Promise((resolve) => setTimeout(resolve, 180));
assert.equal(rebuiltNativeSaveClicks, 1, "automatic saving must rebind after Emby rebuilds the native command");
assert.equal(nativeSaveClicks, 4,
    "a retained hidden native command must no longer receive automatic saves after Emby adds a replacement");
deferNativeConfigurationSaves = true;
ticketLifetimeInput.value = "362";
configurationMain.dispatch("change", { target: ticketLifetimeInput, isTrusted: true });
await new Promise((resolve) => setTimeout(resolve, 180));
assert.equal(rebuiltNativeSaveClicks, 2);
assert.equal(pendingNativeConfigurationSaves.length, 1);
ticketLifetimeInput.value = "363";
configurationMain.dispatch("change", { target: ticketLifetimeInput, isTrusted: true });
await new Promise((resolve) => setTimeout(resolve, 180));
assert.equal(rebuiltNativeSaveClicks, 2,
    "a second Generic UI save must wait for the first request instead of racing it");
pendingNativeConfigurationSaves.shift().resolve({});
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(rebuiltNativeSaveClicks, 3,
    "the newest pending values must save immediately after the in-flight request completes");
pendingNativeConfigurationSaves.shift().resolve({});
await new Promise((resolve) => setTimeout(resolve, 0));
deferNativeConfigurationSaves = false;
rejectNativeConfigurationSave = true;
ticketLifetimeInput.value = "364";
configurationMain.dispatch("change", { target: ticketLifetimeInput, isTrusted: true });
await new Promise((resolve) => setTimeout(resolve, 180));
assert.equal(rebuiltNativeSaveClicks, 4);
rejectNativeConfigurationSave = false;
configurationMain.dispatch("change", { target: ticketLifetimeInput, isTrusted: true });
await new Promise((resolve) => setTimeout(resolve, 180));
assert.equal(rebuiltNativeSaveClicks, 5,
    "a failed native save must not suppress an immediate retry of the same value");
const customConfigurationSection = document.getElementById("embyExternalPlayerCustomPlayers");
assert.ok(customConfigurationSection, "the independent custom-player editor must be added to the plugin page");
assert.equal(customConfigurationSection.parentNode, document.body, "the custom-player editor must be a full-width peer of Generic UI mainContent");
assert.equal(configurationMain.nextSibling, customConfigurationSection, "the independent editor must directly follow the auto-saved basic settings");
const builtInRows = customConfigurationSection.walk().filter((item) =>
    item.className.split(/\s+/).includes("emby-ep-config-builtin-row"));
assert.equal(builtInRows.length, 6, "all built-in players must expose their configurable platform scope");
assert.ok(builtInRows.every((row) => !row.walk().some((item) => item.tagName === "LEGEND")),
    "the section heading must replace repeated platform legends in built-in rows");
const iinaBuiltInRow = builtInRows.find((item) => item.attributes.get("data-player-id") === "Iina");
const iinaPlatformInputs = iinaBuiltInRow.walk().filter((item) =>
    item.className.split(/\s+/).includes("emby-ep-config-platform-input"));
assert.deepEqual(
    iinaPlatformInputs.filter((item) => item.checked).map((item) => item.value),
    ["MacOS"],
    "built-in platform selections must be restored from server configuration");
const iinaIosPlatform = iinaPlatformInputs.find((item) => item.value === "IOS");
iinaIosPlatform.checked = true;
iinaIosPlatform.dispatch("change");
assert.ok(!iinaBuiltInRow.walk().some((item) => item.tagName === "BUTTON"),
    "built-in platform rows must not contain Save buttons");
await new Promise((resolve) => setTimeout(resolve, 300));
assert.deepEqual(lastBuiltInPlatformsPostBody, {
    playerId: "Iina",
    platforms: ["MacOS", "IOS"]
}, "built-in players must save multi-platform selections independently");
rejectBuiltInPlatforms = true;
iinaIosPlatform.checked = false;
iinaIosPlatform.dispatch("change");
await new Promise((resolve) => setTimeout(resolve, 300));
assert.equal(iinaIosPlatform.checked, true,
    "a failed automatic platform save must restore the last server-confirmed selection");
assert.equal(
    iinaBuiltInRow.walk().find((item) =>
        item.className.split(/\s+/).includes("emby-ep-config-card-status")).attributes.get("data-state"),
    "error");
rejectBuiltInPlatforms = false;
const loadedCustomPlayerCard = customConfigurationSection.walk().find((item) =>
    item.className.split(/\s+/).includes("emby-ep-config-card") &&
    !item.className.split(/\s+/).includes("emby-ep-config-builtin-row"));
const loadedPlayerName = loadedCustomPlayerCard.walk().find((item) =>
    item.attributes.get("data-field") === "applicationName");
const loadedPlayerEnabled = loadedCustomPlayerCard.walk().find((item) =>
    item.attributes.get("data-field") === "enabled");
const loadedPlaybackReporting = loadedCustomPlayerCard.walk().find((item) =>
    item.attributes.get("data-field") === "enablePlaybackReporting");
const loadedPlayerEnabledContainer = loadedCustomPlayerCard.walk().find((item) =>
    item.className.split(/\s+/).includes("emby-ep-config-enabled-container"));
const loadedPlayerEnabledSwitch = loadedCustomPlayerCard.walk().find((item) =>
    item.className.split(/\s+/).includes("emby-ep-toggle-switch"));
const loadedPlayerStatus = loadedCustomPlayerCard.walk().find((item) =>
    item.className.split(/\s+/).includes("emby-ep-config-card-status"));
const loadedPlatformInputs = loadedCustomPlayerCard.walk().filter((item) =>
    item.className.split(/\s+/).includes("emby-ep-config-platform-input"));
const loadedPlayerSave = loadedCustomPlayerCard.walk().find((item) =>
    item.tagName === "BUTTON" && item.textContent === "保存此播放器");
assert.equal(
    loadedCustomPlayerCard.walk().find((item) => item.tagName === "LEGEND")?.textContent,
    "适用平台",
    "a standalone custom-player form must retain its one contextual platform label");
assert.equal(loadedPlayerStatus.textContent, "", "a loaded player starts without a false dirty state");
assert.ok(loadedPlayerEnabledContainer.className.includes("toggleContainer"));
assert.ok(loadedPlayerEnabled.className.includes("emby-toggle"));
assert.equal(loadedPlayerEnabled.attributes.get("is"), "emby-toggle");
assert.equal(loadedPlayerEnabled.attributes.get("role"), "switch");
assert.equal(loadedPlayerEnabled.checked, true);
assert.ok(loadedPlaybackReporting, "custom players must expose an explicit playback-reporting switch");
assert.equal(loadedPlaybackReporting.checked, true);
assert.ok(loadedCustomPlayerCard.walk().some((item) =>
    item.textContent.includes("启用播放进度回传")));
assert.ok(loadedPlayerEnabledSwitch.className.includes("toggleSwitch"));
assert.deepEqual(
    loadedPlatformInputs.filter((item) => item.checked).map((item) => item.value),
    ["MacOS", "IOS"],
    "the custom-player editor must restore every selected platform");
const windowsPlatform = loadedPlatformInputs.find((item) => item.value === "Windows");
const iosPlatform = loadedPlatformInputs.find((item) => item.value === "IOS");
iosPlatform.checked = false;
iosPlatform.dispatch("change");
windowsPlatform.checked = true;
windowsPlatform.dispatch("change");
loadedPlayerEnabled.checked = false;
loadedPlayerEnabled.dispatch("change");
assert.equal(loadedPlayerStatus.textContent, "有未保存的更改");
loadedPlayerName.value = "IINA Nova Updated";
loadedPlayerName.dispatch("input");
assert.equal(loadedPlayerStatus.textContent, "有未保存的更改");
assert.equal(loadedPlayerStatus.attributes.get("data-state"), "dirty");
loadedPlayerSave.dispatch("click");
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(lastCustomPlayerPostBody.enabled, false, "the Emby-style switch must preserve the saved Enabled value");
assert.equal(lastCustomPlayerPostBody.enablePlaybackReporting, true,
    "playback reporting must be submitted as an explicit administrator choice");
assert.deepEqual(
    Array.from(lastCustomPlayerPostBody.platforms),
    ["Windows", "MacOS"],
    "custom-player platforms must be submitted as a multi-value selection");
assert.equal(loadedPlayerStatus.textContent, "已保存", "save feedback must stay with the player it applies to");
assert.equal(loadedPlayerStatus.attributes.get("data-state"), "saved");
const originalAjax = apiClient.ajax;
let completeDelayedSave;
apiClient.ajax = function (options) {
    if (String(options.url).includes("ExternalPlayer/CustomPlayers") && options.type === "POST") {
        const value = JSON.parse(options.data);
        return new Promise((resolve) => {
            completeDelayedSave = () => resolve({ ...value, Id: value.id || "saved-custom-player" });
        });
    }
    return originalAjax.call(apiClient, options);
};
loadedPlayerName.value = "Submitted name";
loadedPlayerName.dispatch("input");
loadedPlayerSave.dispatch("click");
loadedPlayerName.value = "Edited while saving";
loadedPlayerName.dispatch("input");
completeDelayedSave();
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(
    loadedPlayerStatus.attributes.get("data-state"),
    "dirty",
    "a stale save response must not mark later edits as saved");
let failDelayedSave;
apiClient.ajax = function (options) {
    if (String(options.url).includes("ExternalPlayer/CustomPlayers") && options.type === "POST") {
        return new Promise((_resolve, reject) => {
            failDelayedSave = () => reject(new Error("old save failed"));
        });
    }
    return originalAjax.call(apiClient, options);
};
loadedPlayerName.value = "Submitted before failure";
loadedPlayerName.dispatch("input");
loadedPlayerSave.dispatch("click");
loadedPlayerName.value = "Edited after failed request";
loadedPlayerName.dispatch("input");
failDelayedSave();
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(
    loadedPlayerStatus.attributes.get("data-state"),
    "dirty",
    "a stale failed request must not overwrite the status of later unsaved edits");
apiClient.ajax = originalAjax;
const addCustomPlayerButton = customConfigurationSection.walk().find((item) =>
    item.tagName === "BUTTON" && item.textContent === "添加播放器");
assert.ok(addCustomPlayerButton);
addCustomPlayerButton.dispatch("click");
addCustomPlayerButton.dispatch("click");
const draftCards = customConfigurationSection.walk().filter((item) =>
    item.className.split(/\s+/).includes("emby-ep-config-card") &&
    !item.className.split(/\s+/).includes("emby-ep-config-builtin-row") &&
    item.attributes.get("data-persisted") === "false");
assert.equal(draftCards.length, 2);
assert.ok(draftCards.every((card) => /^[a-f0-9]{32}$/.test(card.attributes.get("data-player-id"))),
    "new custom-player drafts must use stable client IDs for idempotent retries");
assert.notEqual(draftCards[0].attributes.get("data-player-id"), draftCards[1].attributes.get("data-player-id"));
assert.equal(
    customConfigurationSection.walk().filter((item) =>
        item.className.split(/\s+/).includes("emby-ep-config-card") &&
        !item.className.split(/\s+/).includes("emby-ep-config-builtin-row")).length,
    3,
    "multiple custom-player drafts must be addable without the page Save command");
assert.match(
    invalidOverlay.walk().find((item) => item.className === "emby-ep-error").textContent,
    /无法生成播放地址/);
document.dispatch("keydown", { key: "Escape", preventDefault() {} });

ticketLifetimeInput.value = "365";
configurationMain.dispatch("input", { target: ticketLifetimeInput, isTrusted: true });
window.location.hash = "#!/plugins";
document.dispatch("viewshow", {});
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(rebuiltNativeSaveClicks, 6,
    "leaving the settings route must flush a pending basic-setting edit before teardown");
assert.equal(
    document.getElementById("embyExternalPlayerCustomPlayers"),
    null,
    "leaving the configuration route must remove its independently injected editor");
assert.equal(
    document.getElementById("embyExternalPlayerConfigurationSave"),
    null,
    "the configuration page must remain free of replacement Save buttons");
assert.ok(
    !rebuiltSaveButton.className.includes("emby-ep-native-save-hidden"),
    "leaving the route must restore Emby's retained native Save button");

configurationMain.hidden = true;
rebuiltSaveButton.hidden = true;
const secondConfigurationMain = document.createElement("div");
secondConfigurationMain.className = "mainContent";
document.body.appendChild(secondConfigurationMain);
const secondConfigurationSave = document.createElement("button");
secondConfigurationSave.className = "raised emby-button btnSave pagebutton";
secondConfigurationSave.setAttribute("data-data1", "PageSave");
secondConfigurationSave.textContent = "Save";
document.body.appendChild(secondConfigurationSave);
window.location.hash = "#!/genericui?PageId=f7e75c%3ASettings";
document.dispatch("viewshow", {});
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(document.getElementById("embyExternalPlayerConfigurationSave"), null);
assert.equal(
    window.__embyExternalPlayerModule.configurationSaveButton,
    secondConfigurationSave,
    "automatic saving must bind to the active page after returning");
assert.ok(secondConfigurationSave.className.includes("emby-ep-native-save-hidden"));
assert.ok(
    !rebuiltSaveButton.className.includes("emby-ep-native-save-hidden"),
    "a retained hidden page must not be enhanced together with the active configuration page");
mutationObservers.at(-1).trigger();
assert.equal(
    window.__embyExternalPlayerModule.configurationSaveButton,
    secondConfigurationSave,
    "observer refresh must not bind automatic saving to a retained old page");
let returnedConfigurationSection = document.getElementById("embyExternalPlayerCustomPlayers");
assert.equal(returnedConfigurationSection.walk().filter((item) =>
    item.className.split(/\s+/).includes("emby-ep-config-card") &&
    !item.className.split(/\s+/).includes("emby-ep-config-builtin-row")).length, 3,
"unsaved custom-player drafts must survive leaving and returning to the same server settings page");
currentServerId = "server-b";
document.dispatch("viewshow", {});
await new Promise((resolve) => setTimeout(resolve, 0));
const serverBConfigurationSection = document.getElementById("embyExternalPlayerCustomPlayers");
assert.notEqual(serverBConfigurationSection, returnedConfigurationSection,
    "switching servers must discard the rendered section from the previous server");
assert.equal(serverBConfigurationSection.attributes.get("data-context"), "server-b|user-a");
assert.equal(serverBConfigurationSection.walk().filter((item) =>
    item.className.split(/\s+/).includes("emby-ep-config-card") &&
    !item.className.split(/\s+/).includes("emby-ep-config-builtin-row")).length, 1,
"drafts and custom rows from server A must never appear on server B");
currentServerId = "server-a";
document.dispatch("viewshow", {});
await new Promise((resolve) => setTimeout(resolve, 0));
returnedConfigurationSection = document.getElementById("embyExternalPlayerCustomPlayers");
assert.equal(returnedConfigurationSection.attributes.get("data-context"), "server-a|user-a");
assert.equal(returnedConfigurationSection.walk().filter((item) =>
    item.className.split(/\s+/).includes("emby-ep-config-card") &&
    !item.className.split(/\s+/).includes("emby-ep-config-builtin-row")).length, 3,
"server A drafts must be restored only after returning to server A");

window.location.hash = "#!/item?id=navigation-source";
document.dispatch("viewshow", {});
await new Promise((resolve) => setTimeout(resolve, 0));
button = document.getElementById("embyExternalPlayerButton");
button.dispatch("click");
assert.ok(document.querySelector(".emby-ep-overlay"));
window.location.hash = "#!/item?id=navigation-target";
document.dispatch("viewshow", {});
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(
    document.querySelector(".emby-ep-overlay"),
    null,
    "SPA navigation must close the chooser bound to the previous media item");

console.log("Web module tests passed.");
