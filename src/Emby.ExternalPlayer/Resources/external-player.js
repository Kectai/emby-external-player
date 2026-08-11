define(["events", "connectionManager"], function (events, connectionManager) {
    "use strict";

    var moduleKey = "__embyExternalPlayerModule";
    var buttonId = "embyExternalPlayerButton";
    var selectorProfile = {
        actionRow: ".mainDetailButtons, .detailPagePrimaryContainer .detailButtons",
        playButton: "button.btnPlay, button.btnResume, .btnPlay, .btnResume",
        mediaSource: "select.selectSource",
        subtitle: "select.selectSubtitles"
    };
    var state = window[moduleKey];

    if (state && state.dispose) {
        state.dispose();
    }

    state = {
        observer: null,
        timer: null,
        generation: 0,
        currentItemId: null,
        manifest: null,
        activeDialog: null,
        installed: false,
        connectionSubscribed: false
    };
    window[moduleKey] = state;

    function getItemId() {
        var match = (window.location.hash + window.location.search).match(/[?&]id=([^&]+)/i);
        return match ? decodeURIComponent(match[1]) : null;
    }

    function read(object, name) {
        if (!object) {
            return undefined;
        }

        if (Object.prototype.hasOwnProperty.call(object, name)) {
            return object[name];
        }

        var camelName = name.charAt(0).toLowerCase() + name.slice(1);
        return object[camelName];
    }

    function detectLanguage() {
        var htmlLanguage = document.documentElement && document.documentElement.lang;
        return htmlLanguage || (navigator.languages && navigator.languages[0]) || navigator.language || "en-US";
    }

    function text(manifest, key, fallback) {
        var texts = read(manifest, "Texts") || {};
        var value = read(texts, key);
        return typeof value === "string" && value ? value : fallback;
    }

    function format(value, argument) {
        return String(value).replace("{0}", String(argument));
    }

    function isAllowedLaunchUrl(url, player) {
        if (typeof url !== "string") {
            return false;
        }
        var match = url.match(/^([A-Za-z][A-Za-z0-9+.-]{1,31}):/);
        var schemes = read(player, "LaunchSchemes") || [];
        return !!match && schemes.some(function (scheme) {
            return String(scheme).toLowerCase() === match[1].toLowerCase();
        });
    }

    function getApiClient() {
        if (connectionManager && connectionManager.currentApiClient) {
            return connectionManager.currentApiClient();
        }

        return window.ApiClient;
    }

    function apiGet(path, query) {
        var apiClient = getApiClient();
        return apiClient.getJSON(apiClient.getUrl(path, query || {}));
    }

    function apiPost(path, body) {
        var apiClient = getApiClient();
        return apiClient.ajax({
            type: "POST",
            url: apiClient.getUrl(path),
            data: JSON.stringify(body),
            contentType: "application/json",
            dataType: "json"
        });
    }

    function ensureStylesheet() {
        if (document.getElementById("embyExternalPlayerStyles")) {
            return;
        }

        var link = document.createElement("link");
        link.id = "embyExternalPlayerStyles";
        link.rel = "stylesheet";
        var apiClient = getApiClient();
        if (!apiClient) {
            return;
        }
        link.href = apiClient.getUrl("ExternalPlayer/Web/style.css");
        document.head.appendChild(link);
    }

    function removeButton() {
        var existing = document.getElementById(buttonId);
        if (existing) {
            existing.remove();
        }
    }

    function findActionRow() {
        return document.querySelector(selectorProfile.actionRow);
    }

    function findPlayButton(row) {
        return row.querySelector(selectorProfile.playButton);
    }

    function makeButton(manifest) {
        var button = document.createElement("button");
        button.id = buttonId;
        button.type = "button";
        button.className = "emby-button detailButton emby-external-player-button";
        button.setAttribute("aria-label", read(manifest, "ButtonText") || text(manifest, "ExternalPlay", "External play"));

        var icon = document.createElement("span");
        icon.className = "material-icons detailButton-icon";
        icon.setAttribute("aria-hidden", "true");
        icon.textContent = "open_in_new";

        var label = document.createElement("div");
        label.className = "detailButton-text";
        label.textContent = read(manifest, "ButtonText") || text(manifest, "ExternalPlay", "External play");

        button.appendChild(icon);
        button.appendChild(label);
        button.addEventListener("click", function () {
            openChooser(manifest);
        });
        return button;
    }

    function insertButton(manifest) {
        var players = read(manifest, "Players") || [];
        var mediaSources = read(manifest, "MediaSources") || [];
        if (!manifest || !read(manifest, "Enabled") || !players.length || !mediaSources.length) {
            removeButton();
            return false;
        }

        if (document.getElementById(buttonId)) {
            return true;
        }

        var row = findActionRow();
        if (!row) {
            return false;
        }

        var button = makeButton(manifest);
        var playButton = findPlayButton(row);
        if (read(manifest, "ButtonPlacement") === "AfterPrimaryPlay" && playButton && playButton.parentNode === row) {
            row.insertBefore(button, playButton.nextSibling);
        } else {
            row.appendChild(button);
        }

        return true;
    }

    function stopObserver() {
        if (state.observer) {
            state.observer.disconnect();
            state.observer = null;
        }
        if (state.timer) {
            window.clearTimeout(state.timer);
            state.timer = null;
        }
    }

    function scheduleInjection() {
        var itemId = getItemId();
        var generation = ++state.generation;
        stopObserver();
        removeButton();
        state.currentItemId = itemId;
        state.manifest = null;

        var apiClient = getApiClient();
        if (!itemId || !apiClient || !apiClient.getUrl) {
            return;
        }

        apiGet("ExternalPlayer/Manifest", {
            itemId: itemId,
            platform: detectPlatform(),
            language: detectLanguage()
        })
            .then(function (manifest) {
                if (generation !== state.generation || itemId !== getItemId()) {
                    return;
                }

                state.manifest = manifest;
                if (insertButton(manifest)) {
                    return;
                }

                state.observer = new MutationObserver(function () {
                    if (insertButton(manifest)) {
                        stopObserver();
                    }
                });
                state.observer.observe(document.body, { childList: true, subtree: true });
                state.timer = window.setTimeout(stopObserver, 10000);
            })
            .catch(function () {
                removeButton();
            });
    }

    function detectPlatform() {
        var source = (navigator.userAgentData && navigator.userAgentData.platform) || navigator.platform || navigator.userAgent || "";
        if (/iphone|ipad|ipod/i.test(source)) { return "IOS"; }
        if (/android/i.test(source)) { return "Android"; }
        if (/win/i.test(source)) { return "Windows"; }
        if (/mac/i.test(source)) { return "MacOS"; }
        if (/linux/i.test(source)) { return "Linux"; }
        return "Unknown";
    }

    function appendOption(select, value, label, selected) {
        var option = document.createElement("option");
        option.value = value;
        option.textContent = label;
        option.selected = !!selected;
        select.appendChild(option);
    }

    function makeField(labelText, control) {
        var label = document.createElement("label");
        label.className = "emby-external-player-field";
        var text = document.createElement("span");
        text.textContent = labelText;
        label.appendChild(text);
        label.appendChild(control);
        return label;
    }

    function closeDialog(overlay) {
        if (!overlay) {
            return;
        }

        if (overlay._externalPlayerKeyHandler) {
            document.removeEventListener("keydown", overlay._externalPlayerKeyHandler, true);
        }
        if (overlay && overlay.parentNode) {
            overlay.parentNode.removeChild(overlay);
        }
        if (state.activeDialog === overlay) {
            state.activeDialog = null;
        }
        if (overlay._externalPlayerRestoreFocus && overlay._externalPlayerRestoreFocus.focus) {
            overlay._externalPlayerRestoreFocus.focus();
        }
    }

    function openChooser(manifest) {
        var oldDialog = document.querySelector(".emby-external-player-overlay");
        if (oldDialog) {
            oldDialog.remove();
        }

        var overlay = document.createElement("div");
        overlay.className = "emby-external-player-overlay";
        overlay.setAttribute("role", "presentation");

        var dialog = document.createElement("section");
        dialog.className = "emby-external-player-dialog";
        dialog.setAttribute("role", "dialog");
        dialog.setAttribute("aria-modal", "true");

        var title = document.createElement("h2");
        title.id = "embyExternalPlayerDialogTitle";
        title.textContent = read(manifest, "ItemName") || text(manifest, "ExternalPlay", "External play");
        dialog.setAttribute("aria-labelledby", title.id);
        dialog.appendChild(title);

        var sourceSelect = document.createElement("select");
        var mediaSources = read(manifest, "MediaSources") || [];
        mediaSources.forEach(function (source, index) {
            appendOption(
                sourceSelect,
                read(source, "Id"),
                read(source, "Name") || format(text(manifest, "VersionNumber", "Version {0}"), index + 1),
                read(source, "IsDefault"));
        });
        var pageSourceSelect = document.querySelector(selectorProfile.mediaSource);
        if (pageSourceSelect && mediaSources.some(function (source) {
            return read(source, "Id") === pageSourceSelect.value;
        })) {
            sourceSelect.value = pageSourceSelect.value;
        }
        dialog.appendChild(makeField(text(manifest, "MediaVersion", "Media version"), sourceSelect));

        var subtitleSelect = document.createElement("select");
        function refreshSubtitles() {
            subtitleSelect.textContent = "";
            appendOption(subtitleSelect, "", text(manifest, "NoExternalSubtitle", "Do not load an external subtitle"), true);
            var selectedSource = mediaSources.find(function (source) {
                return read(source, "Id") === sourceSelect.value;
            });
            ((selectedSource && read(selectedSource, "Subtitles")) || []).forEach(function (subtitle) {
                var index = read(subtitle, "Index");
                appendOption(
                    subtitleSelect,
                    String(index),
                    read(subtitle, "DisplayTitle") || read(subtitle, "Language") ||
                        format(text(manifest, "SubtitleNumber", "Subtitle {0}"), index),
                    read(subtitle, "IsDefault"));
            });
            var pageSubtitleSelect = document.querySelector(selectorProfile.subtitle);
            if (pageSubtitleSelect && Array.prototype.some.call(subtitleSelect.options, function (option) {
                return option.value === pageSubtitleSelect.value;
            })) {
                subtitleSelect.value = pageSubtitleSelect.value;
            }
        }
        sourceSelect.addEventListener("change", refreshSubtitles);
        refreshSubtitles();
        dialog.appendChild(makeField(text(manifest, "Subtitle", "Subtitle"), subtitleSelect));

        var resume = document.createElement("input");
        resume.type = "checkbox";
        resume.checked = !!read(manifest, "ResumeByDefault") && read(manifest, "ResumePositionTicks") > 0;
        resume.disabled = !(read(manifest, "ResumePositionTicks") > 0);
        var resumeField = makeField(text(manifest, "ResumeFromLastPosition", "Resume from the last position"), resume);
        resumeField.insertBefore(resume, resumeField.firstChild);
        dialog.appendChild(resumeField);

        var error = document.createElement("div");
        error.className = "emby-external-player-error";
        error.setAttribute("role", "status");
        dialog.appendChild(error);

        var manual = document.createElement("a");
        manual.className = "emby-external-player-manual-link";
        manual.hidden = true;
        manual.textContent = text(manifest, "RetryLaunch", "If the player did not open, select here to retry");
        dialog.appendChild(manual);

        var actions = document.createElement("div");
        actions.className = "emby-external-player-actions";
        (read(manifest, "Players") || []).forEach(function (player) {
            var launch = document.createElement("button");
            launch.type = "button";
            launch.className = "raised button-submit emby-button";
            launch.textContent = read(player, "DisplayName");
            launch.addEventListener("click", function () {
                error.textContent = "";
                launch.disabled = true;
                apiPost("ExternalPlayer/Resolve", {
                    itemId: read(manifest, "ItemId"),
                    mediaSourceId: sourceSelect.value,
                    subtitleStreamIndex: subtitleSelect.value === "" ? null : Number(subtitleSelect.value),
                    resume: resume.checked,
                    playerId: read(player, "Id"),
                    platform: detectPlatform(),
                    language: detectLanguage()
                }).then(function (resolution) {
                    launch.disabled = false;
                    var launchUrl = read(resolution, "LaunchUrl");
                    if (!isAllowedLaunchUrl(launchUrl, player)) {
                        throw new Error(text(manifest, "InvalidLaunchUrl", "The server did not return a safe application URL."));
                    }
                    var warnings = read(resolution, "Warnings") || [];
                    error.textContent = warnings.join(" ");
                    manual.href = launchUrl;
                    manual.hidden = false;
                    window.location.href = launchUrl;
                }).catch(function () {
                    launch.disabled = false;
                    error.textContent = text(
                        manifest,
                        "ResolveError",
                        "Unable to create the playback address. Check permissions, the media version, and the server connection.");
                });
            });
            actions.appendChild(launch);
        });

        var cancel = document.createElement("button");
        cancel.type = "button";
        cancel.className = "emby-button";
        cancel.textContent = text(manifest, "Cancel", "Cancel");
        cancel.addEventListener("click", function () { closeDialog(overlay); });
        actions.appendChild(cancel);
        dialog.appendChild(actions);
        overlay.appendChild(dialog);
        overlay.addEventListener("click", function (event) {
            if (event.target === overlay) {
                closeDialog(overlay);
            }
        });
        overlay._externalPlayerRestoreFocus = document.activeElement;
        overlay._externalPlayerKeyHandler = function (event) {
            if (event.key === "Escape") {
                event.preventDefault();
                closeDialog(overlay);
                return;
            }

            if (event.key !== "Tab") {
                return;
            }

            var focusable = dialog.querySelectorAll("button:not([disabled]), select:not([disabled]), input:not([disabled]), a[href]");
            if (!focusable.length) {
                event.preventDefault();
                return;
            }

            var first = focusable[0];
            var last = focusable[focusable.length - 1];
            if (event.shiftKey && document.activeElement === first) {
                event.preventDefault();
                last.focus();
            } else if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault();
                first.focus();
            }
        };
        document.addEventListener("keydown", overlay._externalPlayerKeyHandler, true);
        document.body.appendChild(overlay);
        state.activeDialog = overlay;
        var firstAction = actions.querySelector("button");
        if (firstAction) {
            firstAction.focus();
        }
    }

    function lifecycleHandler() {
        window.setTimeout(scheduleInjection, 0);
    }

    function install() {
        if (state.installed) {
            return;
        }
        state.installed = true;
        ensureStylesheet();
        window.addEventListener("hashchange", lifecycleHandler);
        window.addEventListener("popstate", lifecycleHandler);
        document.addEventListener("viewshow", lifecycleHandler, true);
        document.addEventListener("viewbeforeshow", lifecycleHandler, true);
        scheduleInjection();
    }

    state.dispose = function () {
        stopObserver();
        removeButton();
        closeDialog(state.activeDialog);
        window.removeEventListener("hashchange", lifecycleHandler);
        window.removeEventListener("popstate", lifecycleHandler);
        document.removeEventListener("viewshow", lifecycleHandler, true);
        document.removeEventListener("viewbeforeshow", lifecycleHandler, true);
        if (state.connectionSubscribed && events.off) {
            events.off(connectionManager, "localusersignedin", lifecycleHandler);
            state.connectionSubscribed = false;
        }
        state.installed = false;
    };

    return function () {
        if (!state.connectionSubscribed) {
            events.on(connectionManager, "localusersignedin", lifecycleHandler);
            state.connectionSubscribed = true;
        }
        install();
    };
});
