(function () {
    "use strict";

    var stateKey = "__embyExternalPlayerBootstrap";
    var cacheRevision = "3";
    var previous = globalThis[stateKey];
    if (previous && previous.dispose) {
        previous.dispose();
    }

    var state = {
        disposed: false,
        repairStarted: false
    };

    function logFailure(error) {
        if (globalThis.console && globalThis.console.warn) {
            globalThis.console.warn("External Player could not refresh Emby's cached app module.", error);
        }
    }

    function isFeatureLoaded() {
        var feature = globalThis.__embyExternalPlayerModule;
        return !!(feature && feature.installed);
    }

    function repairCacheIfNeeded() {
        if (state.disposed || state.repairStarted || isFeatureLoaded()) {
            return;
        }
        state.repairStarted = true;

        if (!globalThis.fetch || !globalThis.urlCacheParam) {
            logFailure(new Error("The cache refresh APIs are unavailable."));
            return;
        }

        var appUrl = "./app.js?" + globalThis.urlCacheParam;
        var storageKey = "emby-external-player-app-cache:" + cacheRevision + ":" +
            globalThis.location.pathname + ":" + globalThis.urlCacheParam;
        try {
            if (globalThis.sessionStorage && globalThis.sessionStorage.getItem(storageKey)) {
                logFailure(new Error("The refreshed app module still did not load the plugin."));
                return;
            }
        } catch (_) {
            // Private browsing can make sessionStorage unavailable.
        }

        globalThis.fetch(appUrl, {
            cache: "reload",
            credentials: "same-origin"
        }).then(function (response) {
            if (!response.ok) {
                throw new Error("Unexpected app.js response status: " + response.status);
            }
            return response.arrayBuffer();
        }).then(function () {
            var reloadSafe = false;
            try {
                if (globalThis.sessionStorage) {
                    globalThis.sessionStorage.setItem(storageKey, "1");
                    reloadSafe = globalThis.sessionStorage.getItem(storageKey) === "1";
                }
            } catch (_) {
                // Avoid a reload loop when private browsing blocks session storage.
            }

            if (reloadSafe && !state.disposed && !isFeatureLoaded() &&
                globalThis.location && globalThis.location.reload) {
                globalThis.location.reload();
            }
        }).catch(logFailure);
    }

    function dispose() {
        if (state.disposed) {
            return;
        }
        state.disposed = true;
        globalThis.document.removeEventListener("appready", repairCacheIfNeeded);
    }

    state.dispose = dispose;
    globalThis[stateKey] = state;
    globalThis.document.addEventListener("appready", repairCacheIfNeeded, { once: true });

    // Embedded clients can execute this deferred helper after appready.
    if (globalThis.Emby && globalThis.Emby.Page) {
        globalThis.setTimeout(repairCacheIfNeeded, 0);
    }
})();
