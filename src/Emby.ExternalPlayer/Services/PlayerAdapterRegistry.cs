using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Emby.ExternalPlayer.Domain;

namespace Emby.ExternalPlayer.Services;

public sealed class PlayerAdapterRegistry
{
    private readonly IReadOnlyDictionary<PlayerId, IPlayerAdapter> adapters;

    public PlayerAdapterRegistry()
    {
        var builtIns = new IPlayerAdapter[]
        {
            new PotPlayerAdapter(),
            new IinaAdapter(),
            new VlcAdapter(),
            new InfuseAdapter(),
            new MpvAdapter(),
            new NPlayerAdapter(),
        };

        adapters = builtIns.ToDictionary(adapter => adapter.Descriptor.BuiltInId!.Value);
    }

    public IReadOnlyCollection<PlayerDescriptor> GetAvailable(
        PluginOptions options,
        ClientPlatform platform,
        bool platformOnly)
    {
        var builtInPlayers = adapters.Values
            .Where(adapter => options.IsPlayerEnabled(adapter.Descriptor.BuiltInId!.Value))
            .Where(adapter => !platformOnly ||
                              platform == ClientPlatform.Unknown ||
                              adapter.Descriptor.Platforms.Contains(platform))
            .Select(adapter => adapter.Describe(platform));

        var customPlayers = ((IEnumerable<CustomPlayerOptions>)(options.CustomPlayers ?? new CustomPlayerOptionsCollection()))
            .Select((custom, index) => CreateCustomDescriptor(custom, index))
            .Where(descriptor => descriptor is not null)
            .Select(descriptor => descriptor!)
            .Where(descriptor => !platformOnly ||
                                 platform == ClientPlatform.Unknown ||
                                 descriptor.Platforms.Contains(platform));

        return builtInPlayers.Concat(customPlayers).ToArray();
    }

    public string BuildLaunchUrl(PlayerId playerId, PlayerLaunchContext context)
    {
        return BuildBuiltInLaunchUrl(playerId, CanonicalizeContext(context));
    }

    public string BuildLaunchUrl(string playerId, PluginOptions options, PlayerLaunchContext context)
    {
        var canonicalContext = CanonicalizeContext(context);
        if (Enum.TryParse(playerId, ignoreCase: true, out PlayerId builtInId) &&
            Enum.IsDefined(typeof(PlayerId), builtInId))
        {
            return BuildBuiltInLaunchUrl(builtInId, canonicalContext);
        }

        if (!TryGetCustomIndex(playerId, out var customIndex) ||
            options.CustomPlayers is null ||
            customIndex < 0 || customIndex >= options.CustomPlayers.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(playerId));
        }

        var custom = options.CustomPlayers[customIndex];
        if (custom is null || !custom.Enabled || !CustomPlayerTemplate.IsValid(custom.UrlTemplate))
        {
            throw new ArgumentOutOfRangeException(nameof(playerId));
        }
        return CustomPlayerTemplate.Render(custom.UrlTemplate, canonicalContext);
    }

    private string BuildBuiltInLaunchUrl(PlayerId playerId, PlayerLaunchContext context)
    {
        if (!adapters.TryGetValue(playerId, out var adapter))
        {
            throw new ArgumentOutOfRangeException(nameof(playerId));
        }
        return adapter.BuildLaunchUrl(context);
    }

    private static PlayerLaunchContext CanonicalizeContext(PlayerLaunchContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (!Uri.TryCreate(context.StreamUrl, UriKind.Absolute, out var streamUri) ||
            (streamUri.Scheme != Uri.UriSchemeHttp && streamUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Stream URL must be absolute.", nameof(context));
        }

        Uri? subtitleUri = null;
        if (!string.IsNullOrWhiteSpace(context.SubtitleUrl) &&
            (!Uri.TryCreate(context.SubtitleUrl, UriKind.Absolute, out subtitleUri) ||
             (subtitleUri.Scheme != Uri.UriSchemeHttp && subtitleUri.Scheme != Uri.UriSchemeHttps)))
        {
            throw new ArgumentException("Subtitle URL must be absolute.", nameof(context));
        }

        var httpRequestHeaders = (context.HttpRequestHeaders ?? Array.Empty<string>()).ToArray();
        if (httpRequestHeaders.Any(header =>
                string.IsNullOrWhiteSpace(header) ||
                header.Length > 1024 ||
                header.IndexOf(':') <= 0 ||
                header.Contains("\r", StringComparison.Ordinal) ||
                header.Contains("\n", StringComparison.Ordinal)))
        {
            throw new ArgumentException("HTTP request headers contain an invalid field.", nameof(context));
        }

        // Pass canonical HTTP(S) URLs to adapters so a caller cannot smuggle literal
        // whitespace into command-like custom protocol arguments (notably PotPlayer).
        return new PlayerLaunchContext
        {
            StreamUrl = streamUri.AbsoluteUri,
            SubtitleUrl = subtitleUri?.AbsoluteUri,
            Title = context.Title,
            HttpRequestHeaders = httpRequestHeaders,
            StartPositionTicks = context.StartPositionTicks,
            Platform = context.Platform,
        };
    }

    private static PlayerDescriptor? CreateCustomDescriptor(CustomPlayerOptions? custom, int index)
    {
        if (custom is null || !custom.Enabled || string.IsNullOrWhiteSpace(custom.ApplicationName) ||
            custom.ApplicationName.Length > 80 || !CustomPlayerTemplate.IsValid(custom.UrlTemplate))
        {
            return null;
        }

        var capabilities = PlayerCapabilities.None;
        if (custom.UrlTemplate.Contains("{start}", StringComparison.Ordinal))
        {
            capabilities |= PlayerCapabilities.StartPosition;
        }
        if (custom.UrlTemplate.Contains("{subtitle}", StringComparison.Ordinal))
        {
            capabilities |= PlayerCapabilities.ExternalSubtitle;
        }
        if (custom.UrlTemplate.Contains("{title}", StringComparison.Ordinal))
        {
            capabilities |= PlayerCapabilities.DisplayTitle;
        }
        if (CustomPlayerTemplate.SupportsHttpRequestHeaders(custom.UrlTemplate))
        {
            capabilities |= PlayerCapabilities.HttpRequestHeaders;
        }

        return new PlayerDescriptor(
            "custom-" + (index + 1).ToString(CultureInfo.InvariantCulture),
            custom.ApplicationName,
            GetCustomPlatforms(custom.Platform),
            capabilities,
            new[] { CustomPlayerTemplate.GetScheme(custom.UrlTemplate) });
    }

    private static IReadOnlyCollection<ClientPlatform> GetCustomPlatforms(CustomPlayerPlatform platform)
    {
        return platform switch
        {
            CustomPlayerPlatform.Windows => new[] { ClientPlatform.Windows },
            CustomPlayerPlatform.MacOS => new[] { ClientPlatform.MacOS },
            CustomPlayerPlatform.IOS => new[] { ClientPlatform.IOS },
            CustomPlayerPlatform.Android => new[] { ClientPlatform.Android },
            CustomPlayerPlatform.Linux => new[] { ClientPlatform.Linux },
            _ => new[]
            {
                ClientPlatform.Windows, ClientPlatform.MacOS, ClientPlatform.IOS,
                ClientPlatform.Android, ClientPlatform.Linux,
            },
        };
    }

    private static bool TryGetCustomIndex(string value, out int index)
    {
        const string prefix = "custom-";
        index = -1;
        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
               int.TryParse(value.Substring(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var oneBased) &&
               oneBased > 0 && (index = oneBased - 1) >= 0;
    }

    private interface IPlayerAdapter
    {
        PlayerDescriptor Descriptor { get; }

        PlayerDescriptor Describe(ClientPlatform platform);

        string BuildLaunchUrl(PlayerLaunchContext context);
    }

    private abstract class PlayerAdapterBase : IPlayerAdapter
    {
        protected PlayerAdapterBase(PlayerDescriptor descriptor)
        {
            Descriptor = descriptor;
        }

        public PlayerDescriptor Descriptor { get; }

        public virtual PlayerDescriptor Describe(ClientPlatform platform) => Descriptor;

        public abstract string BuildLaunchUrl(PlayerLaunchContext context);

        protected static string Encode(string value) => Uri.EscapeDataString(value);

        protected static string Seconds(PlayerLaunchContext context) =>
            Math.Max(0, context.StartPositionTicks / TimeSpan.TicksPerSecond)
                .ToString(CultureInfo.InvariantCulture);
    }

    private sealed class PotPlayerAdapter : PlayerAdapterBase
    {
        public PotPlayerAdapter()
            : base(new PlayerDescriptor(
                PlayerId.PotPlayer,
                "PotPlayer",
                new[] { ClientPlatform.Windows },
                PlayerCapabilities.StartPosition | PlayerCapabilities.ExternalSubtitle,
                new[] { "potplayer" }))
        {
        }

        public override string BuildLaunchUrl(PlayerLaunchContext context)
        {
            var arguments = new List<string> { "/current" };
            if (context.StartPositionTicks > 0)
            {
                arguments.Add("/seek=" + Seconds(context));
            }

            if (!string.IsNullOrWhiteSpace(context.SubtitleUrl))
            {
                arguments.Add("/sub=" + context.SubtitleUrl);
            }

            // PotPlayer maps the scheme payload to its command-line grammar. The
            // media URL is intentionally not percent-encoded as a whole; URI
            // components were canonicalized above and each option is separated by
            // one literal space, matching PotPlayer's /seek and /sub switches.
            return "potplayer://" + context.StreamUrl + " " + string.Join(" ", arguments);
        }
    }

    private sealed class IinaAdapter : PlayerAdapterBase
    {
        public IinaAdapter()
            : base(new PlayerDescriptor(
                PlayerId.Iina,
                "IINA",
                new[] { ClientPlatform.MacOS },
                PlayerCapabilities.StartPosition | PlayerCapabilities.HttpRequestHeaders,
                new[] { "iina" }))
        {
        }

        public override string BuildLaunchUrl(PlayerLaunchContext context)
        {
            var parameters = new List<string> { "url=" + Encode(context.StreamUrl) };
            if (context.StartPositionTicks > 0 || context.HttpRequestHeaders.Count > 0)
            {
                parameters.Add("new_window=1");
            }
            if (context.StartPositionTicks > 0)
            {
                parameters.Add("mpv_start=" + Seconds(context));
            }
            if (context.HttpRequestHeaders.Count > 0)
            {
                parameters.Add("mpv_http-header-fields=" + Encode(string.Join(",", context.HttpRequestHeaders)));
            }

            return "iina://weblink?" + string.Join("&", parameters);
        }
    }

    private sealed class VlcAdapter : PlayerAdapterBase
    {
        public VlcAdapter()
            : base(new PlayerDescriptor(
                PlayerId.Vlc,
                "VLC media player",
                new[] { ClientPlatform.Windows, ClientPlatform.MacOS, ClientPlatform.IOS, ClientPlatform.Android, ClientPlatform.Linux },
                PlayerCapabilities.None,
                new[] { "vlc" }))
        {
        }

        public override string BuildLaunchUrl(PlayerLaunchContext context) =>
            context.Platform == ClientPlatform.IOS
                ? BuildIosLaunchUrl(context)
                : "vlc://" + context.StreamUrl;

        public override PlayerDescriptor Describe(ClientPlatform platform)
        {
            return platform == ClientPlatform.IOS
                ? new PlayerDescriptor(
                    PlayerId.Vlc,
                    "VLC media player",
                    Descriptor.Platforms,
                    PlayerCapabilities.ExternalSubtitle,
                    new[] { "vlc-x-callback" })
                : Descriptor;
        }

        private static string BuildIosLaunchUrl(PlayerLaunchContext context)
        {
            var parameters = new List<string> { "url=" + Encode(context.StreamUrl) };
            if (!string.IsNullOrWhiteSpace(context.SubtitleUrl))
            {
                parameters.Add("sub=" + Encode(context.SubtitleUrl!));
            }

            return "vlc-x-callback://x-callback-url/stream?" + string.Join("&", parameters);
        }
    }

    private sealed class InfuseAdapter : PlayerAdapterBase
    {
        public InfuseAdapter()
            : base(new PlayerDescriptor(
                PlayerId.Infuse,
                "Infuse",
                new[] { ClientPlatform.IOS, ClientPlatform.MacOS },
                PlayerCapabilities.ExternalSubtitle,
                new[] { "infuse" }))
        {
        }

        public override string BuildLaunchUrl(PlayerLaunchContext context)
        {
            var url = "infuse://x-callback-url/play?url=" + Encode(context.StreamUrl);
            return string.IsNullOrWhiteSpace(context.SubtitleUrl)
                ? url
                : url + "&sub=" + Encode(context.SubtitleUrl!);
        }
    }

    private sealed class MpvAdapter : PlayerAdapterBase
    {
        public MpvAdapter()
            : base(new PlayerDescriptor(
                PlayerId.Mpv,
                "mpv",
                new[] { ClientPlatform.Windows, ClientPlatform.MacOS, ClientPlatform.Linux },
                PlayerCapabilities.None,
                new[] { "mpv" }))
        {
        }

        public override string BuildLaunchUrl(PlayerLaunchContext context) =>
            "mpv://play/" + Encode(context.StreamUrl);
    }

    private sealed class NPlayerAdapter : PlayerAdapterBase
    {
        public NPlayerAdapter()
            : base(new PlayerDescriptor(
                PlayerId.NPlayer,
                "nPlayer",
                new[] { ClientPlatform.IOS, ClientPlatform.Android },
                PlayerCapabilities.None,
                new[] { "nplayer-http", "nplayer-https" }))
        {
        }

        public override string BuildLaunchUrl(PlayerLaunchContext context) =>
            "nplayer-" + context.StreamUrl;
    }
}
