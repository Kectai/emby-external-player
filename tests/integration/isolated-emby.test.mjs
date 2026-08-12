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
assert.match(webModule, /__embyExternalPlayerModule/);
assert.match(webModule, /dataType:\s*"json"/);
assert.match(webModule, /isAllowedLaunchUrl/);
assert.match(webModule, /LaunchSchemes/);
assert.match(webModule, /detectLanguage/);
const webStylesheet = await (await fetchChecked("ExternalPlayer/Web/style.css")).text();
assert.match(webStylesheet, /emby-external-player-dialog/);
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
assert.equal(pluginConfiguration.StreamMode, "SecureTicketRelay");
assert.equal(pluginConfiguration.EnableWebButton, true);
assert.equal(pluginConfiguration.UseLocalizedButtonText, true);
assert.ok(Array.isArray(pluginConfiguration.CustomPlayers));
const localizedConfigurationView = await (await api(
    `UI/View?PageId=${encodeURIComponent(configurationPageId)}&ClientLocale=zh-CN`,
    { headers: { ClientLocale: "zh-CN" } })).json();
const localizedConfigurationJson = JSON.stringify(localizedConfigurationView);
assert.match(localizedConfigurationJson, /外部播放器/);
assert.match(localizedConfigurationJson, /IINA/);
assert.match(localizedConfigurationJson, /VLC media player/);
assert.match(webModule, /添加播放器/);

const savedCustomConfig = await (await api("ExternalPlayer/CustomPlayers", {
    method: "POST",
    body: JSON.stringify({
        Enabled: true,
        ApplicationName: "IINA Nova Integration",
        Platform: "MacOS",
        UrlTemplate: "iina-nova://weblink?url={url}&new_window=1&mpv_start={start}"
    })
})).json();
assert.match(savedCustomConfig.Id, /^[a-f0-9]{32}$/);
const customConfigurations = await (await api("ExternalPlayer/CustomPlayers")).json();
assert.ok(customConfigurations.some((entry) => entry.Id === savedCustomConfig.Id));

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
const invalidTicketResponse = await fetch(new URL(
    `ExternalPlayer/Stream/Invalid?api_key=${"A".repeat(43)}`, baseUrl));
assert.equal(invalidTicketResponse.status, 401);

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
const customIinaPlayer = manifest.Players.find((player) => player.DisplayName === "IINA Nova Integration");
assert.ok(customIinaPlayer && customIinaPlayer.LaunchSchemes.includes("iina-nova"));

const source = manifest.MediaSources[0];
const subtitles = source.Subtitles || [];
assert.ok(subtitles.some((subtitle) => subtitle.Format === "srt"));
assert.ok(subtitles.some((subtitle) => subtitle.Format === "ass"));

async function resolve(body, expected = [200]) {
    return api("ExternalPlayer/Resolve", {
        method: "POST",
        body: JSON.stringify({ ItemId: item.Id, Platform: "MacOS", Language: "zh-CN", ...body })
    }, expected);
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
assert.ok(!iinaResolution.LaunchUrl.includes(token));

const iinaUrl = new URL(iinaResolution.LaunchUrl);
const streamUrl = iinaUrl.searchParams.get("url");
assert.ok(streamUrl);
const parsedStreamUrl = new URL(streamUrl);
assert.ok(parsedStreamUrl.pathname.includes("/ExternalPlayer/Stream/"));
assert.match(decodeURIComponent(parsedStreamUrl.pathname), /集成测试|Integration/);
assert.equal(parsedStreamUrl.search, "", "IINA media URL must not expose a ticket in its title.");
const iinaTicketField = iinaUrl.searchParams.get("mpv_http-header-fields") || "";
const iinaTicketMatch = iinaTicketField.match(/^X-Emby-Playback-Ticket: ([A-Za-z0-9_-]{43})$/);
assert.ok(iinaTicketMatch);
const iinaTicket = iinaTicketMatch[1];
const iinaHeaders = { "X-Emby-Playback-Ticket": iinaTicket };

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
const customTicketMatch = customTicketField.match(/^X-Emby-Playback-Ticket: ([A-Za-z0-9_-]{43})$/);
assert.ok(customTicketMatch);
const customTicket = customTicketMatch[1];

await api(`ExternalPlayer/CustomPlayers/${savedCustomConfig.Id}`, { method: "DELETE" });

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
const secondTicketMatch = secondTicketField.match(/^X-Emby-Playback-Ticket: ([A-Za-z0-9_-]{43})$/);
assert.ok(secondTicketMatch);
const secondTicket = secondTicketMatch[1];
const secondRange = await fetch(secondStreamUrl, {
    headers: { "X-Emby-Playback-Ticket": secondTicket, Range: "bytes=32-47" }
});
assert.equal(secondRange.status, 206);
assert.equal((await secondRange.arrayBuffer()).byteLength, 16);

const subtitle = subtitles.find((candidate) => candidate.Format === "srt");
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
assert.match(new URL(subtitleUrl).pathname, /\/subtitle\.srt$/);
const subtitleResponse = await fetch(subtitleUrl);
assert.equal(subtitleResponse.status, 200);
assert.match(subtitleResponse.headers.get("content-disposition") || "", /^inline; filename="[\x20-\x7E]+"$/);
assert.match(await subtitleResponse.text(), /简体中文外挂字幕/);

const assSubtitle = subtitles.find((candidate) => candidate.Format === "ass");
const assResolution = await (await resolve({
    PlayerId: "Infuse",
    MediaSourceId: source.Id,
    SubtitleStreamIndex: assSubtitle.Index,
    Resume: false
})).json();
const assSubtitleUrl = new URL(assResolution.LaunchUrl).searchParams.get("sub");
assert.ok(assSubtitleUrl);
assert.match(new URL(assSubtitleUrl).pathname, /\/subtitle\.ass$/);
const assSubtitleResponse = await fetch(assSubtitleUrl);
assert.equal(assSubtitleResponse.status, 200);
assert.match(await assSubtitleResponse.text(), /\[Script Info\]/);

if (programData) {
    const logBuffer = fs.readFileSync(serverLogPath);
    assert.ok(logBuffer.length >= serverLogStart, "The Emby log unexpectedly rotated during the test.");
    const logText = logBuffer.subarray(serverLogStart).toString("utf8");
    const protectedUrls = [streamUrl, customStreamUrl, secondStreamUrl, subtitleUrl, assSubtitleUrl];
    const tickets = [iinaTicket, customTicket, secondTicket, ...[subtitleUrl, assSubtitleUrl].map((url) => {
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
