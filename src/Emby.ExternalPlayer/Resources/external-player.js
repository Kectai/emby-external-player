define(["events", "connectionManager"], function (events, connectionManager) {
    "use strict";

    var moduleKey = "__embyExternalPlayerModule";
    var buttonId = "embyExternalPlayerButton";
    var configurationPageId = "f7e75c:Settings";
    var resourceVersion = "1.3.0";
    var selectorProfile = {
        actionRow: ".mainDetailButtons, .detailPagePrimaryContainer .detailButtons",
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

    function getConfigurationPageId() {
        var match = (window.location.hash + window.location.search).match(/[?&]PageId=([^&]+)/i);
        return match ? decodeURIComponent(match[1]) : null;
    }

    function configurationSaveText() {
        var language = detectLanguage().toLowerCase();
        if (language.indexOf("zh-hant") === 0 || language.indexOf("zh-tw") === 0 || language.indexOf("zh-hk") === 0) {
            return "儲存";
        }
        if (language.indexOf("zh") === 0) {
            return "保存";
        }
        return "Save";
    }

    function enhanceConfigurationPage() {
        if (getConfigurationPageId() !== configurationPageId) {
            return false;
        }

        var saveButtons = document.querySelectorAll(
            'button[data-data1="PageSave"], input[data-data1="PageSave"], .btnSave.pagebutton');
        if (!saveButtons.length) {
            return false;
        }

        Array.prototype.forEach.call(saveButtons, function (button) {
            var localizedText = configurationSaveText();
            if (button.tagName === "INPUT") {
                button.value = localizedText;
            } else {
                button.textContent = localizedText;
            }
            button.setAttribute("aria-label", localizedText);
        });
        return true;
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
        var apiClient = getApiClient();
        if (!apiClient) {
            return;
        }
        var link = document.getElementById("embyExternalPlayerStyles") || document.createElement("link");
        link.id = "embyExternalPlayerStyles";
        link.rel = "stylesheet";
        link.href = apiClient.getUrl("ExternalPlayer/Web/style.css", { v: resourceVersion });
        link.setAttribute("data-resource-version", resourceVersion);
        if (!link.parentNode) {
            document.head.appendChild(link);
        }
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
        return row.querySelector("button.btnResume, .btnResume") ||
            row.querySelector("button.btnPlay, .btnPlay");
    }

    function makeSvgIcon(pathData, className) {
        var namespace = "http://www.w3.org/2000/svg";
        var svg = document.createElementNS ? document.createElementNS(namespace, "svg") : document.createElement("svg");
        var path = document.createElementNS ? document.createElementNS(namespace, "path") : document.createElement("path");
        if (className) {
            svg.setAttribute("class", className);
        }
        svg.setAttribute("viewBox", "0 0 24 24");
        svg.setAttribute("focusable", "false");
        svg.setAttribute("aria-hidden", "true");
        path.setAttribute("d", pathData);
        svg.appendChild(path);
        return svg;
    }

    function setClass(element, className, enabled) {
        var classes = String(element.className || "").split(/\s+/).filter(Boolean);
        var index = classes.indexOf(className);
        if (enabled && index < 0) {
            classes.push(className);
        } else if (!enabled && index >= 0) {
            classes.splice(index, 1);
        }
        element.className = classes.join(" ");
    }

    function makeButton(manifest) {
        var button = document.createElement("button");
        button.id = buttonId;
        button.type = "button";
        button.className = "raised emby-button detailButton emby-external-player-button";
        button.setAttribute("aria-label", read(manifest, "ButtonText") || text(manifest, "ExternalPlay", "External play"));

        var icon = makeSvgIcon(
            "M19 19H5V5h7V3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7h-2v7ZM14 3v2h3.59l-9.83 9.83 1.41 1.41L19 6.41V10h2V3h-7Z",
            "detailButton-icon emby-external-player-button-icon");

        var label = document.createElement("div");
        label.className = "detailButton-text emby-external-player-button-text";
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
        if (playButton) {
            var referenceClasses = String(playButton.className || "").split(/\s+/);
            setClass(button, "detailButton-primary", referenceClasses.indexOf("detailButton-primary") >= 0);
            setClass(button, "detailButton-stacked", referenceClasses.indexOf("detailButton-stacked") >= 0);
        }
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

        if (getConfigurationPageId() === configurationPageId) {
            if (enhanceConfigurationPage()) {
                return;
            }
            state.observer = new MutationObserver(function () {
                if (enhanceConfigurationPage()) {
                    stopObserver();
                }
            });
            state.observer.observe(document.body, { childList: true, subtree: true });
            state.timer = window.setTimeout(stopObserver, 10000);
            return;
        }

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
        label.className = "selectContainer emby-external-player-field";
        var labelElement = document.createElement("span");
        labelElement.className = "selectLabelText emby-external-player-field-label";
        labelElement.textContent = labelText;
        control.className = (control.className ? control.className + " " : "") + "emby-select";
        label.appendChild(labelElement);
        label.appendChild(control);
        return label;
    }

    function closeDialog(overlay) {
        if (!overlay) {
            return;
        }

        overlay._externalPlayerClosed = true;
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
            closeDialog(oldDialog);
        }

        var overlay = document.createElement("div");
        overlay.className = "dialogContainer emby-external-player-overlay";
        overlay.setAttribute("role", "presentation");

        var dialog = document.createElement("section");
        dialog.className = "dialog formDialog emby-external-player-dialog";
        dialog.setAttribute("role", "dialog");
        dialog.setAttribute("aria-modal", "true");

        var header = document.createElement("header");
        header.className = "formDialogHeader emby-external-player-header";
        var heading = document.createElement("div");
        heading.className = "emby-external-player-heading";
        var title = document.createElement("h2");
        title.id = "embyExternalPlayerDialogTitle";
        title.className = "formDialogHeaderTitle emby-external-player-title";
        title.textContent = text(manifest, "ExternalPlay", "External play");
        var itemName = document.createElement("div");
        itemName.className = "secondaryText emby-external-player-item-name";
        itemName.textContent = read(manifest, "ItemName") || "";
        heading.appendChild(title);
        heading.appendChild(itemName);
        header.appendChild(heading);

        dialog.appendChild(header);
        dialog.setAttribute("aria-labelledby", title.id);

        var content = document.createElement("div");
        content.className = "formDialogContent emby-external-player-content";
        var contentInner = document.createElement("div");
        contentInner.className = "dialogContentInner dialogContentInner-normalbottompadding emby-external-player-content-inner";

        var playerSection = document.createElement("section");
        playerSection.className = "emby-external-player-section";
        var playerTitle = document.createElement("h3");
        playerTitle.className = "emby-external-player-section-title";
        playerTitle.textContent = text(manifest, "ChoosePlayer", "Choose a player");
        playerSection.appendChild(playerTitle);

        var players = read(manifest, "Players") || [];
        var customPlayerCount = players.filter(function (player) { return !!read(player, "IsCustom"); }).length;
        var playerHint = document.createElement("div");
        playerHint.className = "fieldDescription emby-external-player-player-hint";
        playerHint.textContent = customPlayerCount > 0
            ? text(manifest, "CustomPlayerHint", "Custom applications configured in the plugin are shown here.")
            : text(manifest, "NoCustomPlayerHint", "Custom applications can be added in the External Player plugin settings.");
        playerSection.appendChild(playerHint);

        var playerList = document.createElement("div");
        playerList.className = "emby-scroller emby-external-player-player-list";
        playerList.setAttribute("role", "radiogroup");
        playerList.setAttribute("aria-label", playerTitle.textContent);
        var playerOptions = [];
        var selectedPlayer = players[0] || null;

        function selectPlayer(player, option, moveFocus) {
            selectedPlayer = player;
            playerOptions.forEach(function (candidate) {
                var selected = candidate.player === player;
                candidate.option.setAttribute("aria-checked", selected ? "true" : "false");
                setClass(candidate.option, "emby-external-player-option-selected", selected);
            });
            if (moveFocus && option && option.focus) {
                option.focus();
            }
            if (typeof error !== "undefined" && error) {
                error.textContent = "";
            }
            if (typeof manual !== "undefined" && manual) {
                manual.hidden = true;
            }
        }

        players.forEach(function (player, playerIndex) {
            var option = document.createElement("button");
            option.type = "button";
            option.className = "emby-button emby-external-player-option";
            option.setAttribute("role", "radio");
            option.setAttribute("aria-checked", playerIndex === 0 ? "true" : "false");
            option.setAttribute("data-player-id", read(player, "Id"));
            option.title = read(player, "DisplayName");

            var optionText = document.createElement("span");
            optionText.className = "emby-external-player-option-text";
            var optionName = document.createElement("span");
            optionName.className = "emby-external-player-option-name";
            optionName.textContent = read(player, "DisplayName");
            var optionBadge = document.createElement("span");
            optionBadge.className = "emby-external-player-option-badge" +
                (read(player, "IsCustom") ? " emby-external-player-option-badge-custom" : "");
            optionBadge.textContent = read(player, "IsCustom")
                ? text(manifest, "CustomPlayer", "Custom player")
                : text(manifest, "BuiltInPlayer", "Built-in");
            optionText.appendChild(optionName);
            optionText.appendChild(optionBadge);
            option.appendChild(optionText);
            option.appendChild(makeSvgIcon("m9 16.17-3.59-3.58L4 14l5 5 11-11-1.41-1.41Z", "emby-external-player-option-check"));

            option.addEventListener("click", function () {
                selectPlayer(player, option, false);
            });
            option.addEventListener("keydown", function (event) {
                var direction = event.key === "ArrowRight" || event.key === "ArrowDown" ? 1 :
                    event.key === "ArrowLeft" || event.key === "ArrowUp" ? -1 : 0;
                if (!direction) {
                    return;
                }
                event.preventDefault();
                var next = (playerIndex + direction + playerOptions.length) % playerOptions.length;
                selectPlayer(playerOptions[next].player, playerOptions[next].option, true);
            });
            playerOptions.push({ player: player, option: option });
            playerList.appendChild(option);
        });
        if (playerOptions.length) {
            setClass(playerOptions[0].option, "emby-external-player-option-selected", true);
        }
        playerSection.appendChild(playerList);
        contentInner.appendChild(playerSection);

        var fields = document.createElement("div");
        fields.className = "emby-external-player-fields";

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
        fields.appendChild(makeField(text(manifest, "MediaVersion", "Media version"), sourceSelect));

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
        fields.appendChild(makeField(text(manifest, "Subtitle", "Subtitle"), subtitleSelect));

        var resume = document.createElement("input");
        resume.type = "checkbox";
        resume.className = "emby-external-player-resume-checkbox";
        resume.checked = !!read(manifest, "ResumeByDefault") && read(manifest, "ResumePositionTicks") > 0;
        resume.disabled = !(read(manifest, "ResumePositionTicks") > 0);
        var resumeField = document.createElement("label");
        resumeField.className = "emby-external-player-resume";
        resumeField.appendChild(resume);
        var resumeLabel = document.createElement("span");
        resumeLabel.textContent = text(manifest, "ResumeFromLastPosition", "Resume from the last position");
        resumeField.appendChild(resumeLabel);
        fields.appendChild(resumeField);
        contentInner.appendChild(fields);

        var error = document.createElement("div");
        error.className = "emby-external-player-error";
        error.setAttribute("role", "status");
        error.setAttribute("aria-live", "polite");
        contentInner.appendChild(error);

        var manual = document.createElement("a");
        manual.className = "emby-external-player-manual-link";
        manual.hidden = true;
        manual.textContent = text(manifest, "RetryLaunch", "If the player did not open, select here to retry");
        contentInner.appendChild(manual);
        content.appendChild(contentInner);
        dialog.appendChild(content);

        var actions = document.createElement("div");
        actions.className = "formDialogFooter formDialogFooter-flex emby-external-player-actions";

        var launch = document.createElement("button");
        launch.type = "button";
        launch.className = "raised button-submit emby-button formDialogFooterItem emby-external-player-launch";
        launch.textContent = text(manifest, "Open", "Open");
        launch.disabled = !selectedPlayer;
        var resumeUnavailable = resume.disabled;
        function setBusy(busy) {
            launch.disabled = busy || !selectedPlayer;
            sourceSelect.disabled = busy;
            subtitleSelect.disabled = busy;
            resume.disabled = busy || resumeUnavailable;
            playerOptions.forEach(function (candidate) {
                candidate.option.disabled = busy;
            });
            launch.setAttribute("aria-busy", busy ? "true" : "false");
        }
        launch.addEventListener("click", function () {
            var player = selectedPlayer;
            if (!player || launch.disabled) {
                return;
            }
            error.textContent = "";
            manual.hidden = true;
            setBusy(true);
            apiPost("ExternalPlayer/Resolve", {
                itemId: read(manifest, "ItemId"),
                mediaSourceId: sourceSelect.value,
                subtitleStreamIndex: subtitleSelect.value === "" ? null : Number(subtitleSelect.value),
                resume: resume.checked,
                playerId: read(player, "Id"),
                platform: detectPlatform(),
                language: detectLanguage()
            }).then(function (resolution) {
                if (overlay._externalPlayerClosed) {
                    return;
                }
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
                if (overlay._externalPlayerClosed) {
                    return;
                }
                error.textContent = text(
                    manifest,
                    "ResolveError",
                    "Unable to create the playback address. Check permissions, the media version, and the server connection.");
            }).then(function () {
                setBusy(false);
            });
        });
        actions.appendChild(launch);

        var cancel = document.createElement("button");
        cancel.type = "button";
        cancel.className = "raised emby-button formDialogFooterItem emby-external-player-cancel";
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
        if (playerOptions.length) {
            playerOptions[0].option.focus();
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
