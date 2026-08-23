import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const baseUrl = process.env.EMBY_INTEGRATION_BASE;
const expectedVersion = process.env.EMBY_INTEGRATION_VERSION || "4.9.5.0";
const username = process.env.EMBY_INTEGRATION_USER || "integration-admin";
const password = process.env.EMBY_INTEGRATION_PASSWORD || "local-test-only-4.9.5";
const mediaRoot = path.join(projectRoot, ".local/test-media/movies");
const programData = process.env.EMBY_INTEGRATION_PROGRAMDATA;
const dashboardAppPath = process.env.EMBY_INTEGRATION_DASHBOARD_APP;

assert.ok(baseUrl, "EMBY_INTEGRATION_BASE is required; the test never guesses an Emby server.");
const parsedBase = new URL(baseUrl);
assert.ok(
    (parsedBase.hostname === "127.0.0.1" || parsedBase.hostname === "::1" || parsedBase.hostname === "localhost") &&
    parsedBase.port !== "8096",
    "Integration tests are restricted to a non-default loopback port.");
assert.ok(fs.realpathSync(mediaRoot).startsWith(fs.realpathSync(path.join(projectRoot, ".local"))));

if (dashboardAppPath) {
    assert.ok(
        fs.realpathSync(dashboardAppPath).startsWith(fs.realpathSync(path.join(projectRoot, ".local"))),
        "The dashboard fixture must be inside the project-local directory.");
}

const clientIdentity = 'Emby Client="ExternalPlayerIntegration", Device="Node", DeviceId="external-player-integration", Version="1.0.0"';

async function fetchChecked(relativeUrl, options = {}, expected = [200, 204]) {
    const response = await fetch(new URL(relativeUrl, baseUrl), options);
    if (!expected.includes(response.status)) {
        const text = await response.text();
        throw new Error(`${options.method || "GET"} ${relativeUrl} returned ${response.status}: ${text.slice(0, 400)}`);
    }
    return response;
}

const publicInfo = await (await fetchChecked("System/Info/Public")).json();
assert.equal(publicInfo.Version, expectedVersion);

const webModule = await (await fetchChecked("dashboard-ui/modules/embyexternalplayer/plugin.js")).text();
assert.match(webModule, /\.\/language\.js/);
assert.match(webModule, /__embyExternalPlayerModule/);
assert.match(webModule, /dataType:\s*"json"/);
assert.match(webModule, /isAllowedLaunchUrl/);
assert.match(webModule, /LaunchSchemes/);
assert.match(webModule, /detectLanguage/);
const languageModule = await (await fetchChecked("dashboard-ui/modules/embyexternalplayer/language.js")).text();
assert.match(languageModule, /getCurrentLocale/);
assert.match(languageModule, /ExternalPlayer\/ConfigurationStrings/);
const webStylesheet = await (await fetchChecked("ExternalPlayer/Web/style.css")).text();
assert.match(webStylesheet, /emby-ep-dialog/);
if (dashboardAppPath) {
    const dashboardApp = fs.readFileSync(dashboardAppPath, "utf8");
    assert.equal(
        dashboardApp.split("/* Emby.ExternalPlayer bootstrap: 6f784f38 */").length - 1,
        1,
        "The dashboard bootstrap must be injected exactly once.");
}

const serverLogPath = programData ? path.join(programData, "logs/embyserver.txt") : null;
const authenticationLogStart = serverLogPath ? fs.statSync(serverLogPath).size : 0;

const authentication = await (await fetchChecked("Users/AuthenticateByName", {
    method: "POST",
    headers: {
        "Content-Type": "application/json",
        "X-Emby-Authorization": clientIdentity
    },
    body: JSON.stringify({ Username: username, Pw: password })
})).json();
const token = authentication.AccessToken;
const userId = authentication.User.Id;
assert.ok(token && userId);

// Emby itself logs newly issued tokens during AuthenticateByName. The plugin leak
// audit therefore starts after authentication and covers every plugin request.
if (serverLogPath) {
    const deadline = Date.now() + 2000;
    while (!fs.readFileSync(serverLogPath).subarray(authenticationLogStart).toString("utf8").includes(token) &&
           Date.now() < deadline) {
        await new Promise((resolve) => setTimeout(resolve, 25));
    }
    assert.ok(
        fs.readFileSync(serverLogPath).subarray(authenticationLogStart).toString("utf8").includes(token),
        "The authentication log entry was not flushed before the plugin leak audit.");
}
const serverLogStart = serverLogPath ? fs.statSync(serverLogPath).size : 0;

const authenticatedHeaders = {
    "Content-Type": "application/json",
    "X-Emby-Token": token,
    "X-Emby-Authorization": `${clientIdentity}, UserId="${userId}", Token="${token}"`
};

async function api(relativeUrl, options = {}, expected = [200, 204]) {
    return fetchChecked(relativeUrl, {
        ...options,
        headers: { ...authenticatedHeaders, ...(options.headers || {}) }
    }, expected);
}

const plugins = await (await api("Plugins")).json();
const externalPlayerPlugin = plugins.find((plugin) => plugin.Name === "External Player");
assert.ok(externalPlayerPlugin, "The External Player DLL must be loaded by Emby.");
const configurationPageId = `${externalPlayerPlugin.Id.replaceAll("-", "").slice(0, 6)}:Settings`;
const configurationView = await (await api(
    `UI/View?PageId=${encodeURIComponent(configurationPageId)}&ClientLocale=en-US`,
    { headers: { ClientLocale: "en-US" } })).json();
assert.equal(configurationView.PageId, configurationPageId);
const pluginConfiguration = configurationView.EditObjectContainer.Object;
assert.equal(pluginConfiguration.Enabled, true);
assert.equal(pluginConfiguration.StreamMode, undefined, "the token-exposing legacy mode must not be configurable");
assert.equal(pluginConfiguration.EnableWebButton, true);
assert.equal(pluginConfiguration.UseLocalizedButtonText, true);
assert.equal(pluginConfiguration.DefaultPlayerWindows, "PotPlayer");
assert.equal(pluginConfiguration.DefaultPlayerMacOS, "Iina");
assert.equal(pluginConfiguration.DefaultPlayerIOS, "Infuse");
assert.equal(pluginConfiguration.DefaultPlayerAndroid, "Vlc");
assert.ok(Array.isArray(pluginConfiguration.CustomPlayers));
const localizedConfigurationView = await (await api(
    `UI/View?PageId=${encodeURIComponent(configurationPageId)}&ClientLocale=zh-CN`,
    { headers: { ClientLocale: "zh-CN" } })).json();
const localizedConfigurationJson = JSON.stringify(localizedConfigurationView);
assert.match(localizedConfigurationJson, /外部播放器/);
assert.match(localizedConfigurationJson, /IINA/);
assert.match(localizedConfigurationJson, /VLC media player/);
assert.match(webModule, /添加播放器/);
const localizedConfigurationStrings = await (await api(
    "ExternalPlayer/ConfigurationStrings?language=zh-CN")).json();
assert.equal(localizedConfigurationStrings.UseLocalizedButtonTextDescription,
    "使用当前 Emby Web 客户端设置的语言。");
assert.equal(localizedConfigurationStrings.ButtonPlacement, "按钮位置");

const builtInPlatformConfigurations = await (await api(
    "ExternalPlayer/BuiltInPlayerPlatforms")).json();
assert.equal(builtInPlatformConfigurations.length, 6);
assert.deepEqual(
    builtInPlatformConfigurations.find((entry) => entry.PlayerId === "Iina").Platforms,
    ["MacOS"]);
assert.equal(
    builtInPlatformConfigurations.find((entry) => entry.PlayerId === "Iina").Enabled,
    true);
const enabledMpv = await (await api("ExternalPlayer/BuiltInPlayerPlatforms", {
    method: "POST",
    body: JSON.stringify({
        PlayerId: "Mpv",
        Enabled: true,
        Platforms: ["Windows", "MacOS", "Linux"]
    })
})).json();
assert.equal(enabledMpv.Enabled, true);
const expandedIinaPlatforms = await (await api("ExternalPlayer/BuiltInPlayerPlatforms", {
    method: "POST",
    body: JSON.stringify({ PlayerId: "Iina", Platforms: ["MacOS", "IOS"] })
})).json();
assert.deepEqual(expandedIinaPlatforms.Platforms, ["MacOS", "IOS"]);
const rejectedIinaPlatforms = await api("ExternalPlayer/BuiltInPlayerPlatforms", {
    method: "POST",
    body: JSON.stringify({ PlayerId: "Iina", Platforms: ["IOS"] })
}, [400]);
assert.equal(rejectedIinaPlatforms.status, 400,
    "an administrator default must remain available on its configured platform");

const savedCustomConfig = await (await api("ExternalPlayer/CustomPlayers", {
    method: "POST",
    body: JSON.stringify({
        Enabled: true,
        ApplicationName: "IINA Nova Integration",
        Platforms: ["MacOS", "IOS"],
        EnablePlaybackReporting: true,
        UrlTemplate: "iina-nova://weblink?url={url}&new_window=1&mpv_start={start}" +
            "&mpv_sub-file={subtitle}&mpv_http-header-fields={headers}"
    })
})).json();
assert.match(savedCustomConfig.Id, /^[a-f0-9]{32}$/);
assert.deepEqual(savedCustomConfig.Platforms, ["MacOS", "IOS"]);
assert.equal(savedCustomConfig.EnablePlaybackReporting, true);
const rejectedReportingWithoutHeaders = await api("ExternalPlayer/CustomPlayers", {
    method: "POST",
    body: JSON.stringify({
        Enabled: true,
        ApplicationName: "Misconfigured Reporter",
        Platforms: ["MacOS"],
        EnablePlaybackReporting: true,
        UrlTemplate: "misconfigured://open?url={url}"
    })
}, [400]);
assert.equal(rejectedReportingWithoutHeaders.status, 400,
    "playback reporting must require explicit header transport support");
const customConfigurations = await (await api("ExternalPlayer/CustomPlayers")).json();
assert.ok(customConfigurations.some((entry) => entry.Id === savedCustomConfig.Id));

const idempotentCustomId = "0123456789abcdef0123456789abcdef";
const idempotentCustomBody = {
    Id: idempotentCustomId,
    Enabled: true,
    ApplicationName: "Idempotent Integration Player",
    Platforms: ["MacOS"],
    UrlTemplate: "idempotent-player://open?url={url}"
};
const firstIdempotentSave = await (await api("ExternalPlayer/CustomPlayers", {
    method: "POST",
    body: JSON.stringify(idempotentCustomBody)
})).json();
const repeatedIdempotentSave = await (await api("ExternalPlayer/CustomPlayers", {
    method: "POST",
    body: JSON.stringify(idempotentCustomBody)
})).json();
assert.equal(firstIdempotentSave.Id, idempotentCustomId);
assert.equal(repeatedIdempotentSave.Id, idempotentCustomId);
const configurationsAfterRetry = await (await api("ExternalPlayer/CustomPlayers")).json();
assert.equal(configurationsAfterRetry.filter((entry) => entry.Id === idempotentCustomId).length, 1,
    "repeating a create request with the same client ID must update instead of duplicate");
await api(`ExternalPlayer/CustomPlayers/${idempotentCustomId}`, { method: "DELETE" });

const savedSubtitleCustomConfig = await (await api("ExternalPlayer/CustomPlayers", {
    method: "POST",
    body: JSON.stringify({
        Enabled: true,
        ApplicationName: "Custom Subtitle Integration",
        Platforms: ["MacOS"],
        UrlTemplate: "custom-subtitle://open?url={url}&sub={subtitle}"
    })
})).json();
assert.match(savedSubtitleCustomConfig.Id, /^[a-f0-9]{32}$/);

const libraryName = "External Player Integration";
const virtualFolders = await (await api("Library/VirtualFolders/Query")).json();
if (!(virtualFolders.Items || []).some((folder) => folder.Name === libraryName)) {
    await api("Library/VirtualFolders", {
        method: "POST",
        body: JSON.stringify({
            Name: libraryName,
            CollectionType: "movies",
            RefreshLibrary: false,
            Paths: [mediaRoot],
            LibraryOptions: {
                EnableRealtimeMonitor: false,
                EnableChapterImageExtraction: false,
                ExtractChapterImagesDuringLibraryScan: false,
                DownloadImagesInAdvance: false,
                SaveLocalMetadata: false,
                PreferredMetadataLanguage: "en",
                MetadataCountryCode: "US",
                EnableMultiVersionByFiles: true,
                EnableMultiVersionByMetadata: true,
                MinResumeDurationSeconds: 10
            }
        })
    });
}

await api("Library/Refresh", { method: "POST", body: "{}" });

let item;
const deadline = Date.now() + 60000;
while (Date.now() < deadline) {
    const result = await (await api(
        `Users/${userId}/Items?Recursive=true&IncludeItemTypes=Movie&SearchTerm=Integration&Fields=MediaSources,MediaStreams,Path`)).json();
    item = (result.Items || []).find((candidate) => candidate.Name.includes("集成测试") || candidate.Name.includes("Integration"));
    if (item) break;
    await new Promise((resolve) => setTimeout(resolve, 1000));
}
assert.ok(item, "The project-local integration movie was not indexed within 60 seconds.");

await api(`Users/${userId}/Items/${item.Id}/UserData`, {
    method: "POST",
    body: JSON.stringify({ PlaybackPositionTicks: 1200000000, Played: false })
});

const unauthenticatedManifest = await fetch(new URL(`ExternalPlayer/Manifest?itemId=${item.Id}`, baseUrl));
assert.equal(unauthenticatedManifest.status, 401);
const unauthenticatedPreference = await fetch(new URL("ExternalPlayer/UserDefaultPlayer", baseUrl), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ Platform: "MacOS", PlayerId: "Iina" })
});
assert.equal(unauthenticatedPreference.status, 401);
const invalidTicketResponse = await fetch(new URL(
    `ExternalPlayer/Stream/Invalid?api_key=${"A".repeat(43)}`, baseUrl));
assert.equal(invalidTicketResponse.status, 401);

const rejectedEmptyDefault = await api("ExternalPlayer/UserDefaultPlayer", {
    method: "POST",
    body: JSON.stringify({ Platform: "MacOS", PlayerId: "" })
}, [400]);
assert.equal(rejectedEmptyDefault.status, 400);

// Keep repeated local runs deterministic when an earlier run persisted another personal default.
await api("ExternalPlayer/UserDefaultPlayer", {
    method: "POST",
    body: JSON.stringify({ Platform: "MacOS", PlayerId: "Iina" })
});

const manifestResponse = await api(`ExternalPlayer/Manifest?itemId=${item.Id}&platform=MacOS&language=zh-CN`);
const manifest = await manifestResponse.json();
assert.equal(manifest.Enabled, true);
assert.match(manifest.ItemName, /集成测试|Integration/);
assert.equal(manifest.ResumePositionTicks, 1200000000);
assert.equal(manifest.ButtonText, "外部播放");
assert.equal(
  manifest.Texts.Cancel || manifest.Texts.cancel,
  "取消",
  "Chinese Manifest strings must be selected from the requested client language",
);
assert.ok(manifest.MediaSources.length >= 2, "The multi-version movie must expose at least two media sources.");
assert.ok(manifest.Players.some((player) =>
    player.Id === "Iina" && player.DisplayName === "IINA" && player.LaunchSchemes.includes("iina")));
assert.equal(manifest.DefaultPlayerId, "Iina");
const customIinaPlayer = manifest.Players.find((player) => player.DisplayName === "IINA Nova Integration");
assert.ok(customIinaPlayer && customIinaPlayer.LaunchSchemes.includes("iina-nova"));
assert.equal(customIinaPlayer.Id, `custom-${savedCustomConfig.Id}`);
const customSubtitlePlayer = manifest.Players.find((player) => player.DisplayName === "Custom Subtitle Integration");
assert.ok(customSubtitlePlayer && customSubtitlePlayer.SupportsExternalSubtitle);
assert.equal(customSubtitlePlayer.Id, `custom-${savedSubtitleCustomConfig.Id}`);

const savedUserDefault = await (await api("ExternalPlayer/UserDefaultPlayer", {
    method: "POST",
    body: JSON.stringify({ Platform: "MacOS", PlayerId: customIinaPlayer.Id })
})).json();
assert.equal(savedUserDefault.Platform, "MacOS");
assert.equal(savedUserDefault.PlayerId, customIinaPlayer.Id);
const preferredManifest = await (await api(
    `ExternalPlayer/Manifest?itemId=${item.Id}&platform=MacOS&language=zh-CN`)).json();
assert.equal(preferredManifest.DefaultPlayerId, customIinaPlayer.Id);

const rejectedForeignPlatformPreference = await api("ExternalPlayer/UserDefaultPlayer", {
    method: "POST",
    body: JSON.stringify({ Platform: "Windows", PlayerId: customIinaPlayer.Id })
}, [400]);
assert.equal(rejectedForeignPlatformPreference.status, 400);
const rejectedNumericPlatformPreference = await api("ExternalPlayer/UserDefaultPlayer", {
    method: "POST",
    body: JSON.stringify({ Platform: "999", PlayerId: "Iina" })
}, [400]);
assert.equal(rejectedNumericPlatformPreference.status, 400);

const source = manifest.MediaSources[0];
const subtitles = source.Subtitles || [];
assert.ok(subtitles.some((subtitle) => subtitle.Format === "srt"));
assert.ok(subtitles.some((subtitle) => subtitle.Format === "ass"));
const subtitle = subtitles.find((candidate) => candidate.Format === "srt");

async function resolve(body, expected = [200]) {
    return api("ExternalPlayer/Resolve", {
        method: "POST",
        body: JSON.stringify({ ItemId: item.Id, Platform: "MacOS", Language: "zh-CN", ...body })
    }, expected);
}

function parseMpvHeaders(value) {
    return Object.fromEntries((value || "").split(",").map((field) => {
        const colon = field.indexOf(":");
        assert.ok(colon > 0, `invalid mpv header field: ${field}`);
        return [field.slice(0, colon).trim().toLowerCase(), field.slice(colon + 1).trim()];
    }));
}

await resolve({ PlayerId: "UnknownPlayer", MediaSourceId: source.Id, Resume: false }, [400]);
await resolve({ PlayerId: "Iina", MediaSourceId: "foreign-source", Resume: false }, [400]);
await resolve({ PlayerId: "Iina", MediaSourceId: source.Id, SubtitleStreamIndex: 99999, Resume: false }, [400]);

const iinaResolution = await (await resolve({
    PlayerId: "Iina",
    MediaSourceId: source.Id,
    Resume: true
})).json();
assert.match(iinaResolution.LaunchUrl, /^iina:\/\/weblink\?/);
assert.match(iinaResolution.LaunchUrl, /mpv_start=120/);
assert.match(iinaResolution.LaunchUrl, /mpv_http-header-fields=/);
assert.doesNotMatch(iinaResolution.LaunchUrl, /mpv_force-media-title=/);
assert.ok(iinaResolution.TicketExpiresAt);
assert.equal(iinaResolution.PlaybackReporting.ProtocolVersion, 1);
assert.equal(iinaResolution.PlaybackReporting.HeartbeatSeconds, 10);
assert.ok(iinaResolution.PlaybackReporting.TicketExpiresAtUtc);
assert.ok(!iinaResolution.LaunchUrl.includes(token));

const iinaUrl = new URL(iinaResolution.LaunchUrl);
const streamUrl = iinaUrl.searchParams.get("url");
assert.ok(streamUrl);
const parsedStreamUrl = new URL(streamUrl);
assert.ok(parsedStreamUrl.pathname.includes("/ExternalPlayer/Stream/"));
assert.match(decodeURIComponent(parsedStreamUrl.pathname), /集成测试|Integration/);
const launchIdMatch = parsedStreamUrl.pathname.match(/\/ExternalPlayer\/Stream\/([a-f0-9]{32})\//);
assert.ok(launchIdMatch);
const launchId = launchIdMatch[1];
assert.equal(parsedStreamUrl.search, "", "IINA media URL must not expose a ticket in its title.");
const iinaTicketField = iinaUrl.searchParams.get("mpv_http-header-fields") || "";
const parsedIinaHeaders = parseMpvHeaders(iinaTicketField);
const iinaTicket = parsedIinaHeaders["x-emby-playback-ticket"];
const progressTicket = parsedIinaHeaders["x-emby-progress-ticket"];
assert.match(iinaTicket, /^[A-Za-z0-9_-]{43}$/);
assert.match(progressTicket, /^[A-Za-z0-9_-]{43}$/);
assert.equal(parsedIinaHeaders["x-emby-progress-protocol"], "1");
assert.ok(Date.parse(parsedIinaHeaders["x-emby-progress-expires"]));
const iinaHeaders = { "X-Emby-Playback-Ticket": iinaTicket };

async function reportPlayback(kind, payload, ticket = progressTicket, expectedStatus = 200) {
    const response = await fetch(new URL(`ExternalPlayer/Playback/${kind}`, baseUrl), {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
            "X-Emby-Progress-Ticket": ticket
        },
        body: JSON.stringify(payload)
    });
    assert.equal(response.status, expectedStatus);
    return response.json();
}

const startPayload = {
    protocolVersion: 1,
    launchId,
    epoch: 1,
    sequence: 1,
    positionTicks: 1200000000,
    isPaused: false,
    playbackRate: 1,
    clientTimeUtc: new Date().toISOString()
};
const playbackStart = await reportPlayback("Start", startPayload);
assert.equal(playbackStart.Accepted, true);
assert.ok(playbackStart.OwnerRevision > 0);
const progressPayload = {
    ...startPayload,
    ownerRevision: playbackStart.OwnerRevision,
    sequence: 2,
    positionTicks: 1210000000,
    isPaused: true
};
const playbackProgress = await reportPlayback("Progress", progressPayload);
assert.equal(playbackProgress.AcceptedSequence, 2);
const duplicateProgress = await reportPlayback("Progress", progressPayload);
assert.equal(duplicateProgress.AcceptedSequence, 2);
const playbackStopLogStart = serverLogPath ? fs.statSync(serverLogPath).size : 0;
const playbackStop = await reportPlayback("Stop", {
    ...progressPayload,
    sequence: 3,
    positionTicks: 1220000000,
    clientEndReason: "windowClosed"
});
assert.equal(playbackStop.Accepted, true);
assert.equal(playbackStop.Terminal, true);
if (serverLogPath) {
    await new Promise((resolve) => setTimeout(resolve, 1000));
    const stopLog = fs.readFileSync(serverLogPath)
        .subarray(playbackStopLogStart)
        .toString("utf8");
    assert.doesNotMatch(stopLog, /ObjectDisposedException/,
        "PlaybackStopped subscribers must not observe an already-disposed SessionInfo.");
    assert.doesNotMatch(stopLog, /Object name: 'SessionInfo'/,
        "normal Stop must not immediately dispose the synthetic device session.");
    assert.doesNotMatch(stopLog, /NotificationManager\._sessionManager_PlaybackStopped/,
        "normal Stop must not fail in the asynchronous notification handler.");
}
const sessionsAfterStop = await (await api("Sessions")).json();
const syntheticSession = sessionsAfterStop.find((candidate) =>
    candidate.DeviceId === `external-player-${launchId.slice(0, 12)}`);
if (syntheticSession) {
    assert.equal(syntheticSession.NowPlayingItem ?? null, null,
        "the synthetic session may remain idle, but it must no longer be playing the item.");
}
await reportPlayback("Start", startPayload, iinaTicket, 401);
const wrongScopeStream = await fetch(streamUrl, {
    headers: { "X-Emby-Playback-Ticket": progressTicket }
});
assert.equal(wrongScopeStream.status, 401);

const customIinaResolution = await (await resolve({
    PlayerId: customIinaPlayer.Id,
    MediaSourceId: source.Id,
    Resume: true
})).json();
assert.match(customIinaResolution.LaunchUrl, /^iina-nova:\/\/weblink\?/);
assert.doesNotMatch(customIinaResolution.LaunchUrl, /api_key/i);
const customIinaUrl = new URL(customIinaResolution.LaunchUrl);
const customStreamUrl = customIinaUrl.searchParams.get("url");
assert.ok(customStreamUrl);
assert.equal(new URL(customStreamUrl).search, "", "custom IINA-derived players must keep tickets out of the media title");
const customTicketField = customIinaUrl.searchParams.get("mpv_http-header-fields") || "";
const customTicketMatch = customTicketField.match(/(?:^|,)X-Emby-Playback-Ticket: ([A-Za-z0-9_-]{43})(?:,|$)/);
assert.ok(customTicketMatch);
const customTicket = customTicketMatch[1];
assert.equal(customIinaResolution.PlaybackReporting.ProtocolVersion, 1);
assert.match(customTicketField, /(?:^|,)X-Emby-Progress-Ticket: [A-Za-z0-9_-]{43}(?:,|$)/);

const customIinaSubtitleResolution = await (await resolve({
    PlayerId: customIinaPlayer.Id,
    MediaSourceId: source.Id,
    SubtitleStreamIndex: subtitle.Index,
    Resume: false
})).json();
const customIinaSubtitleLaunchUrl = new URL(customIinaSubtitleResolution.LaunchUrl);
const cleanCustomSubtitleUrl = customIinaSubtitleLaunchUrl.searchParams.get("mpv_sub-file");
assert.ok(cleanCustomSubtitleUrl);
assert.equal(new URL(cleanCustomSubtitleUrl).search, "",
    "header-capable custom players must not expose subtitle tickets in the visible file name");
assert.match(new URL(cleanCustomSubtitleUrl).pathname, /\/[^/]+\.srt$/);
const combinedTicketField = customIinaSubtitleLaunchUrl.searchParams.get("mpv_http-header-fields") || "";
const combinedMediaTicketMatch = combinedTicketField.match(
    /(?:^|,)X-Emby-Playback-Ticket: ([A-Za-z0-9_-]{43})(?:,|$)/);
const combinedSubtitleTicketMatch = combinedTicketField.match(
    /(?:^|,)X-Emby-Subtitle-Ticket: ([A-Za-z0-9_-]{43})(?:,|$)/);
assert.ok(combinedMediaTicketMatch);
assert.ok(combinedSubtitleTicketMatch);
const cleanSubtitleResponse = await fetch(cleanCustomSubtitleUrl, {
    headers: { "X-Emby-Subtitle-Ticket": combinedSubtitleTicketMatch[1] }
});
assert.equal(cleanSubtitleResponse.status, 200);
assert.match(await cleanSubtitleResponse.text(), /简体中文外挂字幕/);
const cleanSubtitleWrongScope = await fetch(cleanCustomSubtitleUrl, {
    headers: { "X-Emby-Subtitle-Ticket": combinedMediaTicketMatch[1] }
});
assert.equal(cleanSubtitleWrongScope.status, 401);

await api(`ExternalPlayer/CustomPlayers/${savedCustomConfig.Id}`, { method: "DELETE" });
const fallbackManifest = await (await api(
    `ExternalPlayer/Manifest?itemId=${item.Id}&platform=MacOS&language=zh-CN`)).json();
assert.equal(fallbackManifest.DefaultPlayerId, "Iina");

const customSubtitleResolution = await (await resolve({
    PlayerId: customSubtitlePlayer.Id,
    MediaSourceId: source.Id,
    SubtitleStreamIndex: subtitle.Index,
    Resume: false
})).json();
const customSubtitleLaunchUrl = new URL(customSubtitleResolution.LaunchUrl);
const customSubtitleUrl = customSubtitleLaunchUrl.searchParams.get("sub");
assert.ok(customSubtitleUrl, "a custom {subtitle} template must receive a signed subtitle URL");
assert.match(new URL(customSubtitleUrl).pathname, /\/[^/]+\.srt$/);
await api(`ExternalPlayer/CustomPlayers/${savedSubtitleCustomConfig.Id}`, { method: "DELETE" });

const head = await fetch(streamUrl, { method: "HEAD", headers: iinaHeaders });
assert.equal(head.status, 200);
assert.equal(head.headers.get("accept-ranges"), "bytes");
assert.equal(Number(head.headers.get("content-length")), 2063107);
assert.match(head.headers.get("content-type") || "", /^video\/mp4/);
assert.match(head.headers.get("content-disposition") || "", /^inline; filename="[\x20-\x7E]+"$/);
assert.match(head.headers.get("etag") || "", /^"[0-9a-f]+-[0-9a-f]+"$/);
assert.ok(head.headers.get("last-modified"));
assert.match(head.headers.get("cache-control") || "", /no-store/);
assert.equal((await head.arrayBuffer()).byteLength, 0);

for (const range of ["bytes=0-99", "bytes=1000-1099", "bytes=2000000-2000099"]) {
    const response = await fetch(streamUrl, { headers: { ...iinaHeaders, Range: range } });
    assert.equal(response.status, 206, `Range ${range} must return 206.`);
    assert.equal((await response.arrayBuffer()).byteLength, 100);
    assert.match(response.headers.get("content-range") || "", /^bytes \d+-\d+\/2063107$/);
}

const secondSource = manifest.MediaSources[1];
const secondResolution = await (await resolve({
    PlayerId: "Iina",
    MediaSourceId: secondSource.Id,
    Resume: false
})).json();
const secondStreamUrl = new URL(secondResolution.LaunchUrl).searchParams.get("url");
assert.ok(secondStreamUrl && secondStreamUrl !== streamUrl);
const secondIinaUrl = new URL(secondResolution.LaunchUrl);
const secondTicketField = secondIinaUrl.searchParams.get("mpv_http-header-fields") || "";
const secondTicket = parseMpvHeaders(secondTicketField)["x-emby-playback-ticket"];
assert.match(secondTicket, /^[A-Za-z0-9_-]{43}$/);
const secondRange = await fetch(secondStreamUrl, {
    headers: { "X-Emby-Playback-Ticket": secondTicket, Range: "bytes=32-47" }
});
assert.equal(secondRange.status, 206);
assert.equal((await secondRange.arrayBuffer()).byteLength, 16);

const nonBlockingIinaSubtitleResolution = await (await resolve({
    PlayerId: "Iina",
    MediaSourceId: source.Id,
    SubtitleStreamIndex: subtitle.Index,
    Resume: false
})).json();
assert.match(nonBlockingIinaSubtitleResolution.LaunchUrl, /^iina:\/\/weblink\?/);
assert.ok(nonBlockingIinaSubtitleResolution.Warnings.length > 0);
assert.doesNotMatch(nonBlockingIinaSubtitleResolution.LaunchUrl, /subtitle|sub-file|sub=/i);
const infuseResolution = await (await resolve({
    PlayerId: "Infuse",
    MediaSourceId: source.Id,
    SubtitleStreamIndex: subtitle.Index,
    Resume: false
})).json();
assert.match(infuseResolution.LaunchUrl, /^infuse:\/\/x-callback-url\/play\?/);
assert.ok(!infuseResolution.LaunchUrl.includes(token));
const infuseUrl = new URL(infuseResolution.LaunchUrl);
const subtitleUrl = infuseUrl.searchParams.get("sub");
assert.ok(subtitleUrl);
assert.match(new URL(subtitleUrl).pathname, /\/[^/]+\.srt$/);
const subtitleResponse = await fetch(subtitleUrl);
assert.equal(subtitleResponse.status, 200);
assert.match(
    subtitleResponse.headers.get("content-disposition") || "",
    /^inline; filename="[\x20-\x7E]+"(?:; filename\*=UTF-8''[^\s]+)?$/);
assert.match(await subtitleResponse.text(), /简体中文外挂字幕/);
const subtitleTicket = new URL(subtitleUrl).searchParams.get("api_key");
assert.match(subtitleTicket, /^[A-Za-z0-9_-]{43}$/);
const subtitleTicketOnMediaUrl = new URL(streamUrl);
subtitleTicketOnMediaUrl.searchParams.set("api_key", subtitleTicket);
const wrongScopeMediaResponse = await fetch(subtitleTicketOnMediaUrl);
assert.equal(wrongScopeMediaResponse.status, 401, "a subtitle ticket must not authorize the media stream");
const mediaTicketOnSubtitleUrl = new URL(subtitleUrl);
mediaTicketOnSubtitleUrl.searchParams.set("api_key", iinaTicket);
const wrongScopeSubtitleResponse = await fetch(mediaTicketOnSubtitleUrl);
assert.equal(wrongScopeSubtitleResponse.status, 401, "a media ticket must not authorize the subtitle stream");

const assSubtitle = subtitles.find((candidate) => candidate.Format === "ass");
const assResolution = await (await resolve({
    PlayerId: "Infuse",
    MediaSourceId: source.Id,
    SubtitleStreamIndex: assSubtitle.Index,
    Resume: false
})).json();
const assSubtitleUrl = new URL(assResolution.LaunchUrl).searchParams.get("sub");
assert.ok(assSubtitleUrl);
assert.match(new URL(assSubtitleUrl).pathname, /\/[^/]+\.ass$/);
const assSubtitleResponse = await fetch(assSubtitleUrl);
assert.equal(assSubtitleResponse.status, 200);
assert.match(await assSubtitleResponse.text(), /\[Script Info\]/);

if (programData) {
    const logBuffer = fs.readFileSync(serverLogPath);
    assert.ok(logBuffer.length >= serverLogStart, "The Emby log unexpectedly rotated during the test.");
    const logText = logBuffer.subarray(serverLogStart).toString("utf8");
    const protectedUrls = [streamUrl, customStreamUrl, secondStreamUrl, subtitleUrl, assSubtitleUrl];
    const tickets = [iinaTicket, progressTicket, customTicket, secondTicket, ...[subtitleUrl, assSubtitleUrl].map((url) => {
        const parsed = new URL(url);
        return parsed.searchParams.get("api_key");
    })];
    assert.ok(!logText.includes(token), "The Emby access token must not appear in server logs.");
    for (const ticket of tickets) {
        assert.match(ticket, /^[A-Za-z0-9_-]{43}$/);
        assert.ok(!logText.includes(ticket), "A raw playback ticket must not appear in server logs.");
    }
    for (const protectedUrl of protectedUrls) {
        assert.ok(!logText.includes(protectedUrl), "A full playback URL must not appear in server logs.");
    }
}

console.log(JSON.stringify({
    version: publicInfo.Version,
    webResources: "passed",
    bootstrapCount: dashboardAppPath ? 1 : "not-checked",
    pluginLoaded: true,
    configurationUi: "passed",
    itemType: item.Type,
    mediaSourceCount: manifest.MediaSources.length,
    subtitleFormats: subtitles.map((entry) => entry.Format).sort(),
    resumeSeconds: manifest.ResumePositionTicks / 10000000,
    headStatus: head.status,
    rangeRequests: 4,
    tokenLeakCheck: "passed"
}));
