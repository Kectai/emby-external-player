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

        adapters = builtIns.ToDictionary(adapter => adapter.Descriptor.Id);
    }

    public IReadOnlyCollection<PlayerDescriptor> GetAvailable(
        PluginOptions options,
        ClientPlatform platform,
        bool platformOnly)
    {
        return adapters.Values
            .Where(adapter => options.IsPlayerEnabled(adapter.Descriptor.Id))
            .Where(adapter => !platformOnly ||
                              platform == ClientPlatform.Unknown ||
                              adapter.Descriptor.Platforms.Contains(platform))
            .Select(adapter => adapter.Describe(platform))
            .ToArray();
    }

    public string BuildLaunchUrl(PlayerId playerId, PlayerLaunchContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        if (!adapters.TryGetValue(playerId, out var adapter))
        {
            throw new ArgumentOutOfRangeException(nameof(playerId));
        }

        if (!Uri.TryCreate(context.StreamUrl, UriKind.Absolute, out var streamUri) ||
            (streamUri.Scheme != Uri.UriSchemeHttp && streamUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Stream URL must be absolute.", nameof(context));
        }

        if (!string.IsNullOrWhiteSpace(context.SubtitleUrl) &&
            (!Uri.TryCreate(context.SubtitleUrl, UriKind.Absolute, out var subtitleUri) ||
             (subtitleUri.Scheme != Uri.UriSchemeHttp && subtitleUri.Scheme != Uri.UriSchemeHttps)))
        {
            throw new ArgumentException("Subtitle URL must be absolute.", nameof(context));
        }

        return adapter.BuildLaunchUrl(context);
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
                PlayerCapabilities.StartPosition | PlayerCapabilities.ExternalSubtitle))
        {
        }

        public override string BuildLaunchUrl(PlayerLaunchContext context)
        {
            var url = "potplayer://" + Encode(context.StreamUrl);
            var parameters = new List<string>();
            if (context.StartPositionTicks > 0)
            {
                parameters.Add("current=" + Seconds(context));
            }

            if (!string.IsNullOrWhiteSpace(context.SubtitleUrl))
            {
                parameters.Add("sub=" + Encode(context.SubtitleUrl!));
            }

            return parameters.Count == 0 ? url : url + "?" + string.Join("&", parameters);
        }
    }

    private sealed class IinaAdapter : PlayerAdapterBase
    {
        public IinaAdapter()
            : base(new PlayerDescriptor(
                PlayerId.Iina,
                "IINA",
                new[] { ClientPlatform.MacOS },
                PlayerCapabilities.StartPosition))
        {
        }

        public override string BuildLaunchUrl(PlayerLaunchContext context)
        {
            var parameters = new List<string> { "url=" + Encode(context.StreamUrl) };
            if (context.StartPositionTicks > 0)
            {
                parameters.Add("new_window=1");
                parameters.Add("mpv_start=" + Seconds(context));
            }

            return "iina://weblink?" + string.Join("&", parameters);
        }
    }

    private sealed class VlcAdapter : PlayerAdapterBase
    {
        public VlcAdapter()
            : base(new PlayerDescriptor(
                PlayerId.Vlc,
                "VLC",
                new[] { ClientPlatform.Windows, ClientPlatform.MacOS, ClientPlatform.IOS, ClientPlatform.Android, ClientPlatform.Linux },
                PlayerCapabilities.None))
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
                    "VLC",
                    Descriptor.Platforms,
                    PlayerCapabilities.ExternalSubtitle)
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
                PlayerCapabilities.ExternalSubtitle))
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
                PlayerCapabilities.None))
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
                PlayerCapabilities.None))
        {
        }

        public override string BuildLaunchUrl(PlayerLaunchContext context) =>
            "nplayer-" + context.StreamUrl;
    }
}
