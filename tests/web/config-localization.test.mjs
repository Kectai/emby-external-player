import assert from "node:assert/strict";
import fs from "node:fs";
import vm from "node:vm";

class Element {
    constructor(tagName, id = "", className = "") {
        this.tagName = tagName.toUpperCase();
        this.id = id;
        this.className = className;
        this.children = [];
        this.parentNode = null;
        this.attributes = new Map();
        this.hidden = false;
        this.checked = false;
        this.value = "";
        this._textContent = "";
    }

    appendChild(child) {
        child.parentNode = this;
        this.children.push(child);
        return child;
    }

    remove() {
        if (this.parentNode) {
            this.parentNode.children = this.parentNode.children.filter((child) => child !== this);
            this.parentNode = null;
        }
    }

    querySelector(selector) {
        const state = selector.match(/^\[data-state="([^"]+)"\]$/)?.[1];
        return state
            ? this.walk().find((element) => element.getAttribute("data-state") === state) || null
            : null;
    }

    setAttribute(name, value) {
        this.attributes.set(name, String(value));
    }

    getAttribute(name) {
        return this.attributes.get(name) ?? null;
    }

    getClientRects() {
        return [{}];
    }

    get textContent() {
        return this._textContent + this.children.map((child) => child.textContent).join("");
    }

    set textContent(value) {
        this._textContent = String(value);
        this.children = [];
    }

    get options() {
        return this.children;
    }

    walk() {
        return this.children.flatMap((child) => [child, ...child.walk()]);
    }
}

class Document {
    constructor() {
        this.listeners = new Map();
        this.documentElement = new Element("html");
        this.body = new Element("body");
        this.body.parentNode = this.documentElement;
    }

    getElementById(id) {
        return [this.body, ...this.body.walk()].find((element) => element.id === id) || null;
    }

    querySelectorAll(selector) {
        if (selector === ".mainContent") {
            return this.body.walk().filter((element) =>
                element.className.split(/\s+/).includes("mainContent"));
        }
        return [];
    }

    addEventListener(name, handler) {
        const handlers = this.listeners.get(name) || [];
        handlers.push(handler);
        this.listeners.set(name, handlers);
    }

    removeEventListener(name, handler) {
        const handlers = this.listeners.get(name) || [];
        this.listeners.set(name, handlers.filter((candidate) => candidate !== handler));
    }

    dispatch(name) {
        for (const handler of this.listeners.get(name) || []) {
            handler({});
        }
    }
}

function addToggle(root, id, labelText, descriptionText) {
    const container = root.appendChild(new Element("div", "", "toggleContainer"));
    const label = container.appendChild(new Element("label"));
    const input = label.appendChild(new Element("input", id));
    input.checked = true;
    const labelSpan = label.appendChild(new Element("span"));
    labelSpan.textContent = labelText;
    const description = container.appendChild(new Element("div", "", "fieldDescription toggleFieldDescription"));
    description.textContent = descriptionText;
    return { input, labelSpan, description };
}

const document = new Document();
const mainContent = document.body.appendChild(new Element("div", "", "mainContent"));
const title = mainContent.appendChild(new Element("h1", "", "sectionTitle"));
title.textContent = "External Player";
const editorDescription = mainContent.appendChild(new Element("p", "", "ge-section-description"));
editorDescription.textContent = "English description";
const localizedText = addToggle(
    mainContent,
    "UseLocalizedButtonText",
    "Use localized button text",
    "Use the language selected by the current Emby Web client.");
const placementContainer = mainContent.appendChild(new Element("div", "", "selectContainer"));
const placement = placementContainer.appendChild(new Element("select", "ButtonPlacement"));
placement.setLabel = (value) => {
    placement.renderedLabel = value;
};
placement.value = "AfterPrimaryPlay";
const afterPlay = placement.appendChild(new Element("option"));
afterPlay.value = "AfterPrimaryPlay";
afterPlay.textContent = "After Play / From Beginning";
const endOfRow = placement.appendChild(new Element("option"));
endOfRow.value = "EndOfActionRow";
endOfRow.textContent = "End of the action row";

const observers = [];
class MutationObserver {
    constructor(callback) {
        this.callback = callback;
        this.disconnected = false;
        observers.push(this);
    }

    observe(target, options) {
        this.target = target;
        this.options = options;
    }

    disconnect() {
        this.disconnected = true;
    }
}

const windowListeners = new Map();
const window = {
    location: {
        hash: "#!/genericui?PageId=f7e75c%3ASettings",
        search: ""
    },
    addEventListener(name, handler) {
        const handlers = windowListeners.get(name) || [];
        handlers.push(handler);
        windowListeners.set(name, handlers);
    },
    removeEventListener(name, handler) {
        const handlers = windowListeners.get(name) || [];
        windowListeners.set(name, handlers.filter((candidate) => candidate !== handler));
    },
    clearTimeout,
    setTimeout,
    getComputedStyle() {
        return { display: "block", visibility: "visible" };
    }
};
const navigator = { language: "en-US", languages: ["en-US"] };
let currentEmbyLocale = "en-US";
const globalize = {
    getCurrentLocale() {
        return currentEmbyLocale;
    },
    translate(key) {
        return key === "Delete"
            ? (currentEmbyLocale === "zh-CN" ? "删除" : "Delete")
            : key;
    }
};
const appSettings = {};
let appSettingChangeHandler;
let removedAppSettingChangeHandler;
const events = {
    on(target, name, handler) {
        assert.equal(target, appSettings);
        assert.equal(name, "change");
        appSettingChangeHandler = handler;
    },
    off(target, name, handler) {
        assert.equal(target, appSettings);
        assert.equal(name, "change");
        removedAppSettingChangeHandler = handler;
    }
};
const requestedLanguages = [];
let currentServerId = "server-a";
const catalogs = {
    "en-US": {
        EditorTitle: "External Player",
        EditorDescription: "English editor description",
        UseLocalizedButtonText: "Use localized button text",
        UseLocalizedButtonTextDescription: "Use the language selected by the current Emby Web client.",
        ButtonPlacement: "Button placement",
        AfterPrimaryPlay: "After Play / From Beginning",
        EndOfActionRow: "End of the action row"
    },
    "zh-CN": {
        EditorTitle: "外部播放器",
        EditorDescription: "中文编辑器说明",
        UseLocalizedButtonText: "按钮文字跟随界面语言",
        UseLocalizedButtonTextDescription: "使用当前 Emby Web 客户端设置的语言。",
        ButtonPlacement: "按钮位置",
        AfterPrimaryPlay: "播放/从头开始按钮之后",
        EndOfActionRow: "操作栏末尾"
    }
};
const apiClient = {
    serverId() {
        return currentServerId;
    },
    getUrl(path, query) {
        return { path, query };
    },
    getJSON(request) {
        assert.equal(request.path, "ExternalPlayer/ConfigurationStrings");
        requestedLanguages.push(request.query.language);
        return Promise.resolve(catalogs[request.query.language]);
    }
};
const connectionManager = {
    currentApiClient() {
        return apiClient;
    }
};
let languageModule;
function define(dependencies, factory) {
    assert.deepEqual(Array.from(dependencies), [
        "events",
        "connectionManager",
        "globalize",
        "appSettings"
    ]);
    languageModule = factory(events, connectionManager, globalize, appSettings);
}

const source = fs.readFileSync(
    new URL("../../src/Emby.ExternalPlayer/Resources/external-player-language.js", import.meta.url),
    "utf8");
assert.doesNotMatch(source, /Use the language selected by the current Emby Web client\./,
    "the Web module must not duplicate localized server strings");
vm.runInNewContext(source, { window, document, navigator, MutationObserver, define }, {
    filename: "external-player-language.js"
});
await new Promise((resolve) => setTimeout(resolve, 0));

assert.equal(document.documentElement.lang, "en-US");
assert.equal(languageModule.translate("Delete", "Delete"), "Delete");
assert.equal(localizedText.input.checked, true, "localization must not change setting values");
assert.equal(localizedText.description.textContent, "Use the language selected by the current Emby Web client.");
assert.equal(placement.value, "AfterPrimaryPlay", "localization must not change the selected option");
assert.equal(requestedLanguages.length, 1);
assert.equal(
    languageModule.getCachedConfigurationStrings("en-US", apiClient),
    catalogs["en-US"],
    "the configuration editor must reuse the language module's resolved catalog");
await languageModule.getConfigurationStrings("en-US", apiClient);
assert.equal(requestedLanguages.length, 1,
    "the public configuration catalog API must share the existing request cache");
assert.equal(observers.at(-1).target, mainContent,
    "the steady-state observer must be limited to the active configuration container");

currentEmbyLocale = "zh-CN";
appSettingChangeHandler({}, "language");
await new Promise((resolve) => setTimeout(resolve, 0));

assert.equal(document.documentElement.lang, "zh-CN",
    "the core module must receive Emby's current locale through the shared document language");
assert.equal(languageModule.translate("Delete", "fallback"), "删除",
    "common UI words should reuse Emby's loaded catalog");
assert.equal(title.textContent, "外部播放器");
assert.equal(editorDescription.textContent, "中文编辑器说明");
assert.equal(localizedText.labelSpan.textContent, "按钮文字跟随界面语言");
assert.equal(localizedText.description.textContent, "使用当前 Emby Web 客户端设置的语言。");
assert.equal(placement.renderedLabel, "按钮位置");
assert.equal(afterPlay.textContent, "播放/从头开始按钮之后");
assert.equal(endOfRow.textContent, "操作栏末尾");
assert.equal(localizedText.input.checked, true);
assert.equal(placement.value, "AfterPrimaryPlay");
assert.deepEqual(requestedLanguages, ["en-US", "zh-CN"]);
assert.equal(
    languageModule.getCachedConfigurationStrings("zh-CN", apiClient),
    catalogs["zh-CN"]);

document.dispatch("viewshow");
await new Promise((resolve) => setTimeout(resolve, 0));
assert.deepEqual(requestedLanguages, ["en-US", "zh-CN"],
    "localized strings must be cached per server and language");

currentServerId = "server-b";
document.dispatch("viewshow");
await new Promise((resolve) => setTimeout(resolve, 0));
assert.deepEqual(requestedLanguages, ["en-US", "zh-CN", "zh-CN"],
    "switching Emby servers must not reuse another server's localization response");

languageModule.dispose();
assert.equal(removedAppSettingChangeHandler, appSettingChangeHandler);
assert.ok(observers.every((observer) => observer.disconnected));

console.log("Configuration localization tests passed.");
