define(["events", "connectionManager"], function (events, connectionManager) {
    "use strict";

    var moduleKey = "__embyExternalPlayerModule";
    var buttonId = "embyExternalPlayerButton";
    var state = window[moduleKey];

    if (state && state.dispose) {
        state.dispose();
    }

    state = {
        observer: null,
        timer: null,
        generation: 0,
        currentItemId: null,
        manifest: null
    };
    window[moduleKey] = state;

    function getItemId() {
        var match = (window.location.hash + window.location.search).match(/[?&]id=([^&]+)/i);
        return match ? decodeURIComponent(match[1]) : null;
    }

    function apiGet(path, query) {
        return ApiClient.getJSON(ApiClient.getUrl(path, query || {}));
    }

    function apiPost(path, body) {
        return ApiClient.ajax({
            type: "POST",
            url: ApiClient.getUrl(path),
            data: JSON.stringify(body),
            contentType: "application/json"
        });
    }

    function ensureStylesheet() {
        if (document.getElementById("embyExternalPlayerStyles")) {
            return;
        }

        var link = document.createElement("link");
        link.id = "embyExternalPlayerStyles";
        link.rel = "stylesheet";
        link.href = ApiClient.getUrl("ExternalPlayer/Web/style.css");
        document.head.appendChild(link);
    }

    function removeButton() {
        var existing = document.getElementById(buttonId);
        if (existing) {
            existing.remove();
        }
    }

    function findActionRow() {
        return document.querySelector(".mainDetailButtons, .detailPagePrimaryContainer .detailButtons");
    }

    function findPlayButton(row) {
        return row.querySelector("button.btnPlay, button.btnResume, .btnPlay, .btnResume");
    }

    function makeButton(manifest) {
        var button = document.createElement("button");
        button.id = buttonId;
        button.type = "button";
        button.className = "emby-button detailButton emby-external-player-button";
        button.setAttribute("aria-label", manifest.buttonText || "外部播放");

        var icon = document.createElement("span");
        icon.className = "material-icons detailButton-icon";
        icon.setAttribute("aria-hidden", "true");
        icon.textContent = "open_in_new";

        var label = document.createElement("div");
        label.className = "detailButton-text";
        label.textContent = manifest.buttonText || "外部播放";

        button.appendChild(icon);
        button.appendChild(label);
        button.addEventListener("click", function () {
            openChooser(manifest);
        });
        return button;
    }

    function insertButton(manifest) {
        if (!manifest || !manifest.enabled || !manifest.players || !manifest.players.length) {
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
        if (manifest.buttonPlacement === "AfterPrimaryPlay" && playButton && playButton.parentNode === row) {
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

        if (!itemId || !window.ApiClient || !ApiClient.getUrl) {
            return;
        }

        apiGet("ExternalPlayer/Manifest", { itemId: itemId, platform: detectPlatform() })
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
        if (overlay && overlay.parentNode) {
            overlay.parentNode.removeChild(overlay);
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
        title.textContent = manifest.itemName || "外部播放";
        dialog.appendChild(title);

        var sourceSelect = document.createElement("select");
        (manifest.mediaSources || []).forEach(function (source, index) {
            appendOption(sourceSelect, source.id, source.name || ("版本 " + (index + 1)), source.isDefault);
        });
        dialog.appendChild(makeField("媒体版本", sourceSelect));

        var subtitleSelect = document.createElement("select");
        function refreshSubtitles() {
            subtitleSelect.textContent = "";
            appendOption(subtitleSelect, "", "不加载外挂字幕", true);
            var selectedSource = (manifest.mediaSources || []).find(function (source) {
                return source.id === sourceSelect.value;
            });
            ((selectedSource && selectedSource.subtitles) || []).forEach(function (subtitle) {
                appendOption(subtitleSelect, String(subtitle.index), subtitle.displayTitle || subtitle.language || ("字幕 " + subtitle.index), subtitle.isDefault);
            });
        }
        sourceSelect.addEventListener("change", refreshSubtitles);
        refreshSubtitles();
        dialog.appendChild(makeField("字幕", subtitleSelect));

        var resume = document.createElement("input");
        resume.type = "checkbox";
        resume.checked = !!manifest.resumeByDefault && manifest.resumePositionTicks > 0;
        resume.disabled = !(manifest.resumePositionTicks > 0);
        var resumeField = makeField("从上次位置继续", resume);
        resumeField.insertBefore(resume, resumeField.firstChild);
        dialog.appendChild(resumeField);

        var error = document.createElement("div");
        error.className = "emby-external-player-error";
        error.setAttribute("role", "status");
        dialog.appendChild(error);

        var manual = document.createElement("a");
        manual.className = "emby-external-player-manual-link";
        manual.hidden = true;
        manual.textContent = "若播放器未自动打开，请点此重试";
        dialog.appendChild(manual);

        var actions = document.createElement("div");
        actions.className = "emby-external-player-actions";
        (manifest.players || []).forEach(function (player) {
            var launch = document.createElement("button");
            launch.type = "button";
            launch.className = "raised button-submit emby-button";
            launch.textContent = player.displayName;
            launch.addEventListener("click", function () {
                error.textContent = "";
                launch.disabled = true;
                apiPost("ExternalPlayer/Resolve", {
                    itemId: manifest.itemId,
                    mediaSourceId: sourceSelect.value,
                    subtitleStreamIndex: subtitleSelect.value === "" ? null : Number(subtitleSelect.value),
                    resume: resume.checked,
                    playerId: player.id,
                    platform: detectPlatform()
                }).then(function (resolution) {
                    launch.disabled = false;
                    manual.href = resolution.launchUrl;
                    manual.hidden = false;
                    window.location.href = resolution.launchUrl;
                }).catch(function () {
                    launch.disabled = false;
                    error.textContent = "无法生成播放地址，请检查权限、媒体版本或服务器连接。";
                });
            });
            actions.appendChild(launch);
        });

        var cancel = document.createElement("button");
        cancel.type = "button";
        cancel.className = "emby-button";
        cancel.textContent = "取消";
        cancel.addEventListener("click", function () { closeDialog(overlay); });
        actions.appendChild(cancel);
        dialog.appendChild(actions);
        overlay.appendChild(dialog);
        overlay.addEventListener("click", function (event) {
            if (event.target === overlay) {
                closeDialog(overlay);
            }
        });
        document.body.appendChild(overlay);
    }

    function lifecycleHandler() {
        window.setTimeout(scheduleInjection, 0);
    }

    function install() {
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
        window.removeEventListener("hashchange", lifecycleHandler);
        window.removeEventListener("popstate", lifecycleHandler);
        document.removeEventListener("viewshow", lifecycleHandler, true);
        document.removeEventListener("viewbeforeshow", lifecycleHandler, true);
    };

    return function () {
        events.on(connectionManager, "localusersignedin", lifecycleHandler);
        install();
    };
});
