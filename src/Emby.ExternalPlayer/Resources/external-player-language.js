define([
    "events",
    "connectionManager",
    "./../common/globalize.js",
    "./../common/appsettings.js"
], function (events, connectionManager, globalizeModule, appSettingsModule) {
    "use strict";

    var pageId = "f7e75c:Settings";
    var globalize = globalizeModule && globalizeModule.default || globalizeModule;
    var appSettings = appSettingsModule && appSettingsModule.default || appSettingsModule;
    var previous = window.__embyExternalPlayerLanguageModule;
    if (previous && previous.dispose) {
        previous.dispose();
    }

    var fields = {
        Enabled: ["Enabled", "EnabledDescription"],
        EnableWebButton: ["EnableWebButton", "EnableWebButtonDescription"],
        UseLocalizedButtonText: ["UseLocalizedButtonText", "UseLocalizedButtonTextDescription"],
        ButtonText: ["ButtonText", "ButtonTextDescription"],
        ButtonPlacement: ["ButtonPlacement"],
        ShowOnlyPlatformPlayers: ["ShowOnlyPlatformPlayers"],
        ResumeByDefault: ["ResumeByDefault"],
        TicketLifetimeMinutes: ["TicketLifetimeMinutes", "TicketLifetimeMinutesDescription"],
        DefaultPlayerWindows: ["DefaultPlayerWindows"],
        DefaultPlayerMacOS: ["DefaultPlayerMacOS"],
        DefaultPlayerIOS: ["DefaultPlayerIOS"],
        DefaultPlayerAndroid: ["DefaultPlayerAndroid"]
    };
    var state = {
        observer: null,
        observedRoot: null,
        refreshTimer: null,
        requestGeneration: 0,
        strings: Object.create(null),
        disposed: false
    };

    function getLanguage() {
        try {
            var current = globalize && globalize.getCurrentLocale && globalize.getCurrentLocale();
            if (current) {
                return current;
            }
        } catch (_) {
            // Continue with the same browser fallbacks used by Emby Web.
        }
        return (document.documentElement && document.documentElement.getAttribute("data-culture")) ||
            navigator.language ||
            navigator.userLanguage ||
            (navigator.languages && navigator.languages[0]) ||
            "en-US";
    }

    function syncDocumentLanguage() {
        var language = getLanguage();
        if (document.documentElement && document.documentElement.lang !== language) {
            document.documentElement.lang = language;
        }
        return language;
    }

    function translate(key, fallback) {
        try {
            var translator = globalize && (globalize.translate || globalize.getString);
            var translated = translator && translator.call(globalize, key);
            if (translated && translated !== key) {
                return translated;
            }
        } catch (_) {
            // Keep the plugin fallback when the shared Emby catalog is unavailable.
        }
        return fallback;
    }

    function getPageId() {
        var match = (window.location.hash + window.location.search).match(/[?&]PageId=([^&]+)/i);
        if (!match) {
            return null;
        }
        try {
            return decodeURIComponent(match[1]);
        } catch (_) {
            return null;
        }
    }

    function hasClass(element, name) {
        return String(element && element.className || "").split(/\s+/).indexOf(name) >= 0;
    }

    function isVisible(element) {
        for (var current = element; current && current !== document; current = current.parentNode) {
            if (current.hidden || current.getAttribute && current.getAttribute("aria-hidden") === "true" ||
                hasClass(current, "hide")) {
                return false;
            }
            if (window.getComputedStyle) {
                var style = window.getComputedStyle(current);
                if (style && (style.display === "none" || style.visibility === "hidden")) {
                    return false;
                }
            }
        }
        return !element.getClientRects || element.getClientRects().length > 0;
    }

    function getActiveMainContent() {
        var elements = document.querySelectorAll(".mainContent");
        for (var index = elements.length - 1; index >= 0; index--) {
            if (isVisible(elements[index])) {
                return elements[index];
            }
        }
        return elements.length ? elements[elements.length - 1] : null;
    }

    function isDescendant(root, element) {
        for (var current = element; current; current = current.parentNode) {
            if (current === root) {
                return true;
            }
        }
        return false;
    }

    function findByClass(root, name) {
        if (!root) {
            return null;
        }
        if (hasClass(root, name)) {
            return root;
        }
        var children = root.children || [];
        for (var index = 0; index < children.length; index++) {
            var match = findByClass(children[index], name);
            if (match) {
                return match;
            }
        }
        return null;
    }

    function setText(element, text) {
        if (element && typeof text === "string" && element.textContent !== text) {
            element.textContent = text;
        }
    }

    function findFieldContainer(root, field) {
        for (var current = field.parentNode; current && current !== root; current = current.parentNode) {
            if (hasClass(current, "toggleContainer") || hasClass(current, "inputContainer") ||
                hasClass(current, "selectContainer")) {
                return current;
            }
        }
        return null;
    }

    function setFieldLabel(field, label, container) {
        if (field.getAttribute && field.getAttribute("label") !== label) {
            field.setAttribute("label", label);
        }
        if (typeof field.label === "function" && field.labelElement) {
            field.label(label);
            return;
        }
        if (typeof field.setLabel === "function") {
            field.setLabel(label);
            return;
        }
        var parent = field.parentNode;
        if (parent && String(parent.tagName).toUpperCase() === "LABEL") {
            var siblings = parent.children || [];
            for (var index = 0; index < siblings.length; index++) {
                if (String(siblings[index].tagName).toUpperCase() === "SPAN" &&
                    !hasClass(siblings[index], "toggleSwitch")) {
                    setText(siblings[index], label);
                    return;
                }
            }
        }
        setText(findByClass(container, "inputLabel") || findByClass(container, "selectLabelText"), label);
    }

    function localizeField(root, id, keys, strings) {
        var field = document.getElementById(id);
        if (!field || !isDescendant(root, field)) {
            return;
        }
        var container = findFieldContainer(root, field);
        setFieldLabel(field, strings[keys[0]], container);
        if (keys.length > 1) {
            setText(findByClass(container, "fieldDescription"), strings[keys[1]]);
        }
    }

    function localizeOptions(root, id, optionKeys, strings) {
        var field = document.getElementById(id);
        if (!field || !isDescendant(root, field)) {
            return;
        }
        var options = field.options || [];
        for (var index = 0; index < options.length; index++) {
            var key = optionKeys[options[index].value];
            if (key) {
                setText(options[index], strings[key]);
            }
        }
    }

    function getApiClient() {
        return connectionManager && connectionManager.currentApiClient
            ? connectionManager.currentApiClient()
            : window.ApiClient;
    }

    function getApiContext() {
        var apiClient = getApiClient();
        if (!(apiClient && apiClient.getJSON && apiClient.getUrl)) {
            return null;
        }
        var serverId = "";
        try {
            serverId = typeof apiClient.serverId === "function"
                ? apiClient.serverId()
                : apiClient.serverId;
        } catch (_) {
            return null;
        }
        if (!serverId) {
            try {
                serverId = apiClient.getUrl("");
            } catch (_) {
                return null;
            }
        }
        return { apiClient: apiClient, key: String(serverId) };
    }

    function loadStrings(language, context) {
        var cacheKey = context.key + "|" + String(language).toLowerCase();
        if (!state.strings[cacheKey]) {
            state.strings[cacheKey] = context.apiClient.getJSON(context.apiClient.getUrl(
                "ExternalPlayer/ConfigurationStrings",
                { language: language })).catch(function (error) {
                    delete state.strings[cacheKey];
                    throw error;
                });
        }
        return state.strings[cacheKey];
    }

    function observe(root) {
        if (state.observedRoot === root) {
            return;
        }
        if (state.observer) {
            state.observer.disconnect();
        }
        state.observedRoot = root;
        state.observer = new MutationObserver(scheduleRefresh);
        state.observer.observe(root, { childList: true, subtree: true });
    }

    function localize() {
        if (state.disposed || getPageId() !== pageId) {
            return;
        }
        var root = getActiveMainContent();
        if (!root) {
            return;
        }
        var readyField = document.getElementById("UseLocalizedButtonText");
        if (readyField && isDescendant(root, readyField)) {
            observe(root);
        }
        var language = syncDocumentLanguage();
        var context = getApiContext();
        if (!context) {
            return;
        }
        var generation = ++state.requestGeneration;
        loadStrings(language, context).then(function (strings) {
            var currentContext = getApiContext();
            if (state.disposed || generation !== state.requestGeneration ||
                getPageId() !== pageId || getActiveMainContent() !== root ||
                !currentContext || currentContext.key !== context.key ||
                getLanguage().toLowerCase() !== language.toLowerCase()) {
                return;
            }
            setText(findByClass(root, "sectionTitle"), strings.EditorTitle);
            setText(findByClass(root, "ge-section-description"), strings.EditorDescription);
            Object.keys(fields).forEach(function (id) {
                localizeField(root, id, fields[id], strings);
            });
            localizeOptions(root, "ButtonPlacement", {
                AfterPrimaryPlay: "AfterPrimaryPlay",
                EndOfActionRow: "EndOfActionRow"
            }, strings);
        }).catch(function () {
            // Emby's server-rendered strings remain usable when localization cannot be refreshed.
        });
    }

    function scheduleRefresh() {
        window.clearTimeout(state.refreshTimer);
        state.refreshTimer = window.setTimeout(localize, 0);
    }

    function refresh() {
        if (state.disposed) {
            return;
        }
        syncDocumentLanguage();
        if (getPageId() !== pageId) {
            if (state.observer) {
                state.observer.disconnect();
                state.observer = null;
                state.observedRoot = null;
            }
            state.requestGeneration++;
            return;
        }
        if (document.body) {
            observe(document.body);
        }
        scheduleRefresh();
    }

    function onAppSettingChanged(_, name) {
        if (name === "language") {
            var section = document.getElementById("embyExternalPlayerCustomPlayers");
            if (section && !section.querySelector('[data-state="dirty"]') &&
                !section.querySelector('[data-state="saving"]')) {
                section.remove();
            }
            refresh();
        }
    }

    function dispose() {
        if (state.disposed) {
            return;
        }
        state.disposed = true;
        state.requestGeneration++;
        window.clearTimeout(state.refreshTimer);
        state.refreshTimer = null;
        if (state.observer) {
            state.observer.disconnect();
            state.observer = null;
            state.observedRoot = null;
        }
        window.removeEventListener("hashchange", refresh);
        window.removeEventListener("popstate", refresh);
        document.removeEventListener("viewshow", refresh, true);
        document.removeEventListener("viewbeforeshow", refresh, true);
        if (events && events.off && appSettings) {
            events.off(appSettings, "change", onAppSettingChanged);
        }
    }

    window.addEventListener("hashchange", refresh);
    window.addEventListener("popstate", refresh);
    document.addEventListener("viewshow", refresh, true);
    document.addEventListener("viewbeforeshow", refresh, true);
    if (events && events.on && appSettings) {
        events.on(appSettings, "change", onAppSettingChanged);
    }
    state.dispose = dispose;
    state.translate = translate;
    window.__embyExternalPlayerLanguageModule = state;
    refresh();
    return state;
});
