define(["events", "connectionManager"], function (events, connectionManager) {
    "use strict";

    var moduleKey = "__embyExternalPlayerModule";
    var buttonId = "embyExternalPlayerButton";
    var configurationPageId = "f7e75c:Settings";
    var resourceVersion = "1.4.4";
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

    function configurationTexts() {
        var language = detectLanguage().toLowerCase();
        if (language.indexOf("zh-hant") === 0 || language.indexOf("zh-tw") === 0 || language.indexOf("zh-hk") === 0) {
            return {
                pageSave: "儲存",
                title: "自訂播放器",
                description: "自訂播放器在此獨立儲存，不需要使用頁面底部的儲存按鈕。URL 範本支援 {url}、{title}、{subtitle}、{start} 和 {headers}。",
                add: "新增播放器",
                enabled: "啟用",
                applicationName: "官方應用程式名稱",
                platform: "平台",
                anyPlatform: "所有平台",
                urlTemplate: "URL Scheme 範本",
                save: "儲存此播放器",
                remove: "刪除",
                saved: "已儲存",
                removed: "已刪除",
                loadError: "無法載入自訂播放器設定。",
                saveError: "無法儲存，請檢查應用程式名稱和 URL 範本。",
                deleteConfirm: "確定刪除這個自訂播放器嗎？"
            };
        }
        if (language.indexOf("zh") === 0) {
            return {
                pageSave: "保存",
                title: "自定义播放器",
                description: "自定义播放器在这里独立保存，不需要使用页面底部的保存按钮。URL 模板支持 {url}、{title}、{subtitle}、{start} 和 {headers}。",
                add: "添加播放器",
                enabled: "启用",
                applicationName: "官方应用名称",
                platform: "平台",
                anyPlatform: "所有平台",
                urlTemplate: "URL Scheme 模板",
                save: "保存此播放器",
                remove: "删除",
                saved: "已保存",
                removed: "已删除",
                loadError: "无法加载自定义播放器配置。",
                saveError: "无法保存，请检查应用名称和 URL 模板。",
                deleteConfirm: "确定删除这个自定义播放器吗？"
            };
        }
        return {
            pageSave: "Save",
            title: "Custom players",
            description: "Custom players are saved independently here; the page Save button is not required. Templates support {url}, {title}, {subtitle}, {start}, and {headers}.",
            add: "Add player",
            enabled: "Enabled",
            applicationName: "Official application name",
            platform: "Platform",
            anyPlatform: "Any platform",
            urlTemplate: "URL scheme template",
            save: "Save player",
            remove: "Delete",
            saved: "Saved",
            removed: "Deleted",
            loadError: "Unable to load custom-player settings.",
            saveError: "Unable to save. Check the application name and URL template.",
            deleteConfirm: "Delete this custom player?"
        };
    }

    function ensureLocalizedConfigurationSaveButton(originalButton) {
        setClass(originalButton, "emby-external-player-native-save-hidden", true);
        originalButton.setAttribute("aria-hidden", "true");
        originalButton.setAttribute("tabindex", "-1");

        var replacement = document.getElementById("embyExternalPlayerConfigurationSave");
        if (!replacement) {
            var localizedText = configurationTexts().pageSave;
            replacement = document.createElement("button");
            replacement.id = "embyExternalPlayerConfigurationSave";
            replacement.type = "button";
            replacement.className = "raised button-submit block emby-button emby-external-player-main-save";
            replacement.textContent = localizedText;
            replacement.setAttribute("aria-label", localizedText);
            replacement.addEventListener("click", function () {
                var currentOriginal = replacement._externalPlayerOriginalSave;
                if (currentOriginal && currentOriginal.click) {
                    currentOriginal.click();
                }
            });
        }
        replacement._externalPlayerOriginalSave = originalButton;
        if (replacement.parentNode !== originalButton.parentNode || originalButton.nextSibling !== replacement) {
            originalButton.parentNode.insertBefore(replacement, originalButton.nextSibling);
        }
    }

    function enhanceConfigurationPage() {
        if (getConfigurationPageId() !== configurationPageId) {
            return false;
        }

        var saveButtons = document.querySelectorAll(
            'button[data-data1="PageSave"], input[data-data1="PageSave"], .btnSave.pagebutton');
        Array.prototype.forEach.call(saveButtons, function (button) {
            ensureLocalizedConfigurationSaveButton(button);
        });

        var mainContent = document.querySelector(".mainContent");
        if (mainContent) {
            ensureCustomPlayerConfiguration(mainContent);
        }
        return saveButtons.length > 0 || !!mainContent;
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

    function apiDelete(path) {
        var apiClient = getApiClient();
        return apiClient.ajax({
            type: "DELETE",
            url: apiClient.getUrl(path),
            dataType: "json"
        });
    }

    function makeConfigurationField(labelText, control) {
        var label = document.createElement("label");
        label.className = "inputContainer emby-external-player-config-field";
        var caption = document.createElement("span");
        caption.className = "inputLabel inputLabelUnfocused";
        caption.textContent = labelText;
        label.appendChild(caption);
        label.appendChild(control);
        return label;
    }

    function ensureCustomPlayerConfiguration(mainContent) {
        if (document.getElementById("embyExternalPlayerCustomPlayers")) {
            return;
        }

        var strings = configurationTexts();
        var section = document.createElement("section");
        section.id = "embyExternalPlayerCustomPlayers";
        section.className = "verticalSection emby-external-player-config-section";

        var header = document.createElement("div");
        header.className = "emby-external-player-config-header";
        var headingBox = document.createElement("div");
        var heading = document.createElement("h2");
        heading.className = "sectionTitle emby-external-player-config-title";
        heading.textContent = strings.title;
        var description = document.createElement("div");
        description.className = "fieldDescription emby-external-player-config-description";
        description.textContent = strings.description;
        headingBox.appendChild(heading);
        headingBox.appendChild(description);
        header.appendChild(headingBox);

        var addButton = document.createElement("button");
        addButton.type = "button";
        addButton.className = "raised emby-button emby-external-player-config-add";
        addButton.textContent = strings.add;
        header.appendChild(addButton);
        section.appendChild(header);

        var list = document.createElement("div");
        list.className = "emby-external-player-config-list";
        section.appendChild(list);
        var status = document.createElement("div");
        status.className = "fieldDescription emby-external-player-config-status";
        status.setAttribute("role", "status");
        status.setAttribute("aria-live", "polite");
        section.appendChild(status);
        var mainContentParent = mainContent.parentNode;
        if (mainContentParent) {
            mainContentParent.insertBefore(section, mainContent.nextSibling);
        } else {
            mainContent.appendChild(section);
        }

        function renderCard(player) {
            player = player || {};
            var card = document.createElement("div");
            card.className = "verticalSection emby-external-player-config-card";
            card.setAttribute("data-player-id", read(player, "Id") || "");

            var cardHeader = document.createElement("div");
            cardHeader.className = "emby-external-player-config-card-header";
            var cardTitle = document.createElement("h3");
            cardTitle.className = "emby-external-player-config-card-title";
            cardTitle.textContent = read(player, "ApplicationName") || strings.add;
            cardHeader.appendChild(cardTitle);
            card.appendChild(cardHeader);

            var fields = document.createElement("div");
            fields.className = "emby-external-player-config-fields";

            var enabled = document.createElement("input");
            enabled.type = "checkbox";
            enabled.className = "emby-external-player-config-enabled";
            enabled.checked = read(player, "Enabled") !== false;
            enabled.setAttribute("data-field", "enabled");
            var enabledLabel = document.createElement("label");
            enabledLabel.className = "emby-external-player-config-enabled-label";
            enabledLabel.appendChild(enabled);
            var enabledText = document.createElement("span");
            enabledText.textContent = strings.enabled;
            enabledLabel.appendChild(enabledText);
            fields.appendChild(enabledLabel);

            var applicationName = document.createElement("input");
            applicationName.type = "text";
            applicationName.className = "emby-input emby-external-player-config-input";
            applicationName.value = read(player, "ApplicationName") || "";
            applicationName.maxLength = 80;
            applicationName.setAttribute("data-field", "applicationName");
            fields.appendChild(makeConfigurationField(strings.applicationName, applicationName));

            var platform = document.createElement("select");
            platform.className = "emby-select emby-external-player-config-select";
            platform.setAttribute("data-field", "platform");
            ["Any", "Windows", "MacOS", "IOS", "Android", "Linux"].forEach(function (value) {
                var option = document.createElement("option");
                option.value = value;
                option.textContent = value === "Any" ? strings.anyPlatform : value;
                platform.appendChild(option);
            });
            platform.value = read(player, "Platform") || "Any";
            fields.appendChild(makeConfigurationField(strings.platform, platform));

            var urlTemplate = document.createElement("input");
            urlTemplate.type = "text";
            urlTemplate.className = "emby-input emby-external-player-config-input";
            urlTemplate.value = read(player, "UrlTemplate") || "";
            urlTemplate.setAttribute("data-field", "urlTemplate");
            fields.appendChild(makeConfigurationField(strings.urlTemplate, urlTemplate));
            card.appendChild(fields);

            var actions = document.createElement("div");
            actions.className = "emby-external-player-config-card-actions";
            var saveButton = document.createElement("button");
            saveButton.type = "button";
            saveButton.className = "raised button-submit emby-button";
            var saveText = document.createElement("span");
            saveText.className = "buttonText";
            saveText.textContent = strings.save;
            saveButton.appendChild(saveText);
            var deleteButton = document.createElement("button");
            deleteButton.type = "button";
            deleteButton.className = "raised emby-button";
            var deleteText = document.createElement("span");
            deleteText.className = "buttonText";
            deleteText.textContent = strings.remove;
            deleteButton.appendChild(deleteText);
            actions.appendChild(saveButton);
            actions.appendChild(deleteButton);
            card.appendChild(actions);

            applicationName.addEventListener("input", function () {
                cardTitle.textContent = applicationName.value || strings.add;
            });
            saveButton.addEventListener("click", function () {
                saveButton.disabled = true;
                status.textContent = "";
                apiPost("ExternalPlayer/CustomPlayers", {
                    id: card.getAttribute("data-player-id") || "",
                    enabled: enabled.checked,
                    applicationName: applicationName.value,
                    platform: platform.value,
                    urlTemplate: urlTemplate.value
                }).then(function (saved) {
                    card.setAttribute("data-player-id", read(saved, "Id") || "");
                    status.textContent = strings.saved;
                }).catch(function () {
                    status.textContent = strings.saveError;
                }).then(function () {
                    saveButton.disabled = false;
                });
            });
            deleteButton.addEventListener("click", function () {
                var id = card.getAttribute("data-player-id") || "";
                if (id && window.confirm && !window.confirm(strings.deleteConfirm)) {
                    return;
                }
                deleteButton.disabled = true;
                var deletion = id
                    ? apiDelete("ExternalPlayer/CustomPlayers/" + encodeURIComponent(id))
                    : Promise.resolve();
                deletion.then(function () {
                    card.remove();
                    status.textContent = strings.removed;
                }).catch(function () {
                    deleteButton.disabled = false;
                    status.textContent = strings.saveError;
                });
            });

            list.appendChild(card);
            return card;
        }

        addButton.addEventListener("click", function () {
            var card = renderCard({ Enabled: true, Platform: "Any" });
            var nameInput = card.querySelector('[data-field="applicationName"]');
            if (nameInput && nameInput.focus) {
                nameInput.focus();
            }
        });

        apiGet("ExternalPlayer/CustomPlayers").then(function (players) {
            (players || []).forEach(renderCard);
        }).catch(function () {
            status.textContent = strings.loadError;
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
        var existing = document.querySelectorAll("#" + buttonId);
        Array.prototype.forEach.call(existing, function (button) {
            button.remove();
        });
    }

    function isVisibleElement(element) {
        if (!element) {
            return false;
        }

        var current = element;
        while (current && current !== document) {
            var classes = String(current.className || "").split(/\s+/);
            if (current.hidden || current.getAttribute && current.getAttribute("aria-hidden") === "true" ||
                classes.indexOf("hide") >= 0) {
                return false;
            }
            if (window.getComputedStyle) {
                var style = window.getComputedStyle(current);
                if (style && (style.display === "none" || style.visibility === "hidden")) {
                    return false;
                }
            }
            current = current.parentNode;
        }

        return !element.getClientRects || element.getClientRects().length > 0;
    }

    function findActionRow() {
        var rows = document.querySelectorAll(selectorProfile.actionRow);
        for (var index = rows.length - 1; index >= 0; index--) {
            if (isVisibleElement(rows[index])) {
                return rows[index];
            }
        }
        return rows.length ? rows[rows.length - 1] : null;
    }

    function findPlayButton(row) {
        return row.querySelector("button.btnMainPlay:not(.hide), .btnMainPlay:not(.hide)") ||
            row.querySelector("button.btnPlay:not(.hide), .btnPlay:not(.hide)") ||
            row.querySelector("button.btnMainPlay, .btnMainPlay") ||
            row.querySelector("button.btnPlay, .btnPlay") ||
            row.querySelector("button.btnResume:not(.hide), .btnResume:not(.hide)") ||
            row.querySelector("button.btnResume, .btnResume");
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

    function syncButtonClasses(button, referenceButton) {
        var ignored = {
            btnPlay: true,
            btnMainPlay: true,
            btnResume: true,
            hide: true,
            detailButtonHighres3: true
        };
        var sourceClasses = String((referenceButton && referenceButton.className) || "raised detailButton emby-button")
            .split(/\s+/)
            .filter(Boolean)
            .filter(function (className) {
                return !ignored[className] &&
                    className.indexOf("detailButton-highres") !== 0 &&
                    className.indexOf("detailButton-lowres") !== 0;
            });
        ["raised", "detailButton", "emby-button", "emby-external-player-button"].forEach(function (className) {
            if (sourceClasses.indexOf(className) < 0) {
                sourceClasses.push(className);
            }
        });
        var value = sourceClasses.join(" ");
        if (button.className !== value) {
            button.className = value;
        }
    }

    function makeButton(manifest, referenceButton) {
        var button = document.createElement("button");
        button.id = buttonId;
        button.type = "button";
        syncButtonClasses(button, referenceButton);
        button.setAttribute("aria-label", read(manifest, "ButtonText") || text(manifest, "ExternalPlay", "External play"));

        var icon = document.createElement("i");
        icon.className = "md-icon md-icon-fill button-icon button-icon-left autortl emby-external-player-button-icon";
        icon.textContent = "\ue89e";

        var label = document.createElement("span");
        label.className = "emby-external-player-button-text";
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

        var row = findActionRow();
        if (!row) {
            return false;
        }

        var playButton = findPlayButton(row);
        var existingButtons = document.querySelectorAll("#" + buttonId);
        var button = null;
        Array.prototype.forEach.call(existingButtons, function (candidate) {
            if (!button || candidate.parentNode === row) {
                if (button && button !== candidate) {
                    button.remove();
                }
                button = candidate;
            } else {
                candidate.remove();
            }
        });
        if (!button) {
            button = makeButton(manifest, playButton);
        } else {
            syncButtonClasses(button, playButton);
        }
        if (read(manifest, "ButtonPlacement") === "AfterPrimaryPlay" && playButton && playButton.parentNode === row) {
            if (button.parentNode !== row || playButton.nextSibling !== button) {
                row.insertBefore(button, playButton.nextSibling);
            }
        } else if (button.parentNode !== row) {
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
            enhanceConfigurationPage();
            state.observer = new MutationObserver(function () {
                enhanceConfigurationPage();
            });
            state.observer.observe(document.body, { childList: true, subtree: true });
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
                insertButton(manifest);
                state.observer = new MutationObserver(function () {
                    if (generation === state.generation && itemId === getItemId()) {
                        insertButton(manifest);
                    }
                });
                state.observer.observe(document.body, {
                    attributes: true,
                    attributeFilter: ["class"],
                    childList: true,
                    subtree: true
                });
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
        dialog.setAttribute("tabindex", "-1");

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
        var launchText = document.createElement("span");
        launchText.className = "buttonText";
        launchText.textContent = text(manifest, "Open", "Open");
        launch.appendChild(launchText);
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
        var cancelText = document.createElement("span");
        cancelText.className = "buttonText";
        cancelText.textContent = text(manifest, "Cancel", "Cancel");
        cancel.appendChild(cancelText);
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
        if (dialog.focus) {
            dialog.focus();
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
