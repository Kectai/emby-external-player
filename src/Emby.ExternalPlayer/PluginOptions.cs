using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Emby.ExternalPlayer.Domain;
using Emby.ExternalPlayer.Localization;
using Emby.ExternalPlayer.Services;
using Emby.Web.GenericEdit;
using MediaBrowser.Model.Attributes;
using Emby.Web.GenericEdit.Validation;
using MediaBrowser.Model.LocalizationAttributes;

namespace Emby.ExternalPlayer;

public enum ButtonPlacement
{
    [DescriptionL(nameof(PluginStrings.AfterPrimaryPlay), typeof(PluginStrings))]
    AfterPrimaryPlay,

    [DescriptionL(nameof(PluginStrings.EndOfActionRow), typeof(PluginStrings))]
    EndOfActionRow,
}

public enum PlayerId
{
    [DescriptionL(nameof(PluginStrings.PotPlayer), typeof(PluginStrings))]
    PotPlayer,

    [DescriptionL(nameof(PluginStrings.IINA), typeof(PluginStrings))]
    Iina,

    [DescriptionL(nameof(PluginStrings.VLCMediaPlayer), typeof(PluginStrings))]
    Vlc,

    [DescriptionL(nameof(PluginStrings.Infuse), typeof(PluginStrings))]
    Infuse,

    [DescriptionL(nameof(PluginStrings.Mpv), typeof(PluginStrings))]
    Mpv,

    [DescriptionL(nameof(PluginStrings.NPlayer), typeof(PluginStrings))]
    NPlayer,
}

public sealed class UserPlayerPreferenceOptions
{
    public string UserId { get; set; } = string.Empty;

    public ClientPlatform Platform { get; set; }

    public string PlayerId { get; set; } = string.Empty;
}

public sealed class UserPlayerPreferenceOptionsCollection : List<UserPlayerPreferenceOptions>
{
    public UserPlayerPreferenceOptionsCollection()
    {
    }

    public UserPlayerPreferenceOptionsCollection(IEnumerable<UserPlayerPreferenceOptions> preferences)
        : base(preferences)
    {
    }
}

public sealed class PluginOptions : EditableOptionsBase
{
    public override string EditorTitle => PluginStrings.EditorTitle;

    public override string EditorDescription => PluginStrings.EditorDescription;

    [DisplayNameL(nameof(PluginStrings.Enabled), typeof(PluginStrings))]
    [DescriptionL(nameof(PluginStrings.EnabledDescription), typeof(PluginStrings))]
    public bool Enabled { get; set; } = true;

    [DisplayNameL(nameof(PluginStrings.EnableWebButton), typeof(PluginStrings))]
    [DescriptionL(nameof(PluginStrings.EnableWebButtonDescription), typeof(PluginStrings))]
    public bool EnableWebButton { get; set; } = true;

    [DisplayNameL(nameof(PluginStrings.UseLocalizedButtonText), typeof(PluginStrings))]
    [DescriptionL(nameof(PluginStrings.UseLocalizedButtonTextDescription), typeof(PluginStrings))]
    public bool UseLocalizedButtonText { get; set; } = true;

    [DisplayNameL(nameof(PluginStrings.ButtonText), typeof(PluginStrings))]
    [DescriptionL(nameof(PluginStrings.ButtonTextDescription), typeof(PluginStrings))]
    public string ButtonText { get; set; } = PluginStrings.ExternalPlay;

    [DisplayNameL(nameof(PluginStrings.ButtonPlacement), typeof(PluginStrings))]
    public ButtonPlacement ButtonPlacement { get; set; } = ButtonPlacement.AfterPrimaryPlay;

    [DisplayNameL(nameof(PluginStrings.ShowOnlyPlatformPlayers), typeof(PluginStrings))]
    public bool ShowOnlyPlatformPlayers { get; set; } = true;

    [DisplayNameL(nameof(PluginStrings.ResumeByDefault), typeof(PluginStrings))]
    public bool ResumeByDefault { get; set; } = true;

    [DisplayNameL(nameof(PluginStrings.TicketLifetimeMinutes), typeof(PluginStrings))]
    [DescriptionL(nameof(PluginStrings.TicketLifetimeMinutesDescription), typeof(PluginStrings))]
    [MinValue(LaunchTicketStore.MinimumLifetimeMinutes)]
    [MaxValue(LaunchTicketStore.MaximumLifetimeMinutes)]
    public int TicketLifetimeMinutes { get; set; } = LaunchTicketStore.DefaultLifetimeMinutes;

    [Browsable(false)]
    public bool EnablePotPlayer { get; set; } = true;

    [Browsable(false)]
    public bool EnableIina { get; set; } = true;

    [Browsable(false)]
    public bool EnableVlc { get; set; } = true;

    [Browsable(false)]
    public bool EnableInfuse { get; set; } = true;

    [Browsable(false)]
    public bool EnableMpv { get; set; }

    [Browsable(false)]
    public bool EnableNPlayer { get; set; }

    [Browsable(false)]
    public PlayerPlatforms PotPlayerPlatformScope { get; set; } = PlayerPlatforms.Windows;

    [Browsable(false)]
    public PlayerPlatforms IinaPlatformScope { get; set; } = PlayerPlatforms.MacOS;

    [Browsable(false)]
    public PlayerPlatforms VlcPlatformScope { get; set; } = PlayerPlatforms.All;

    [Browsable(false)]
    public PlayerPlatforms InfusePlatformScope { get; set; } =
        PlayerPlatforms.MacOS | PlayerPlatforms.IOS;

    [Browsable(false)]
    public PlayerPlatforms MpvPlatformScope { get; set; } =
        PlayerPlatforms.Windows | PlayerPlatforms.MacOS | PlayerPlatforms.Linux;

    [Browsable(false)]
    public PlayerPlatforms NPlayerPlatformScope { get; set; } =
        PlayerPlatforms.IOS | PlayerPlatforms.Android;

    [DisplayNameL(nameof(PluginStrings.DefaultPlayerWindows), typeof(PluginStrings))]
    public PlayerId DefaultPlayerWindows { get; set; } = PlayerId.PotPlayer;

    [DisplayNameL(nameof(PluginStrings.DefaultPlayerMacOS), typeof(PluginStrings))]
    public PlayerId DefaultPlayerMacOS { get; set; } = PlayerId.Iina;

    [DisplayNameL(nameof(PluginStrings.DefaultPlayerIOS), typeof(PluginStrings))]
    public PlayerId DefaultPlayerIOS { get; set; } = PlayerId.Infuse;

    [DisplayNameL(nameof(PluginStrings.DefaultPlayerAndroid), typeof(PluginStrings))]
    public PlayerId DefaultPlayerAndroid { get; set; } = PlayerId.Vlc;

    [Browsable(false)]
    public CustomPlayerOptionsCollection CustomPlayers { get; set; } = new();

    [Browsable(false)]
    public UserPlayerPreferenceOptionsCollection UserPlayerPreferences { get; set; } = new();

    protected override void Validate(ValidationContext context)
    {
        if (!UseLocalizedButtonText && string.IsNullOrWhiteSpace(ButtonText))
        {
            context.AddValidationError(nameof(ButtonText), PluginStrings.ButtonTextRequired);
        }

        if (ButtonText?.Length > 40)
        {
            context.AddValidationError(nameof(ButtonText), PluginStrings.ButtonTextTooLong);
        }

        if (TicketLifetimeMinutes < 30 || TicketLifetimeMinutes > 720)
        {
            context.AddValidationError(
                nameof(TicketLifetimeMinutes),
                PluginStrings.TicketLifetimeInvalid);
        }

        ValidateDefaultPlayer(context, DefaultPlayerWindows, nameof(DefaultPlayerWindows), ClientPlatform.Windows);
        ValidateDefaultPlayer(context, DefaultPlayerMacOS, nameof(DefaultPlayerMacOS), ClientPlatform.MacOS);
        ValidateDefaultPlayer(context, DefaultPlayerIOS, nameof(DefaultPlayerIOS), ClientPlatform.IOS);
        ValidateDefaultPlayer(context, DefaultPlayerAndroid, nameof(DefaultPlayerAndroid), ClientPlatform.Android);

        var customPlayers = CustomPlayers ?? new CustomPlayerOptionsCollection();
        for (var index = 0; index < customPlayers.Count; index++)
        {
            var custom = customPlayers[index];
            if (custom is null || !custom.Enabled)
            {
                continue;
            }
            var propertyName = nameof(CustomPlayers) + "[" + index + "]";
            if (string.IsNullOrWhiteSpace(custom.ApplicationName))
            {
                context.AddValidationError(propertyName, PluginStrings.CustomPlayerNameRequired);
            }
            else if (custom.ApplicationName.Length > 80)
            {
                context.AddValidationError(propertyName, PluginStrings.CustomPlayerNameTooLong);
            }

            if (string.IsNullOrWhiteSpace(custom.UrlTemplate))
            {
                context.AddValidationError(propertyName, PluginStrings.CustomPlayerTemplateRequired);
            }
            else if (!CustomPlayerTemplate.IsValid(custom.UrlTemplate))
            {
                context.AddValidationError(propertyName, PluginStrings.CustomPlayerTemplateInvalid);
            }
        }
    }

    public bool IsPlayerEnabled(PlayerId playerId)
    {
        return playerId switch
        {
            PlayerId.PotPlayer => EnablePotPlayer,
            PlayerId.Iina => EnableIina,
            PlayerId.Vlc => EnableVlc,
            PlayerId.Infuse => EnableInfuse,
            PlayerId.Mpv => EnableMpv,
            PlayerId.NPlayer => EnableNPlayer,
            _ => false,
        };
    }

    public void SetPlayerEnabled(PlayerId playerId, bool enabled)
    {
        switch (playerId)
        {
            case PlayerId.PotPlayer: EnablePotPlayer = enabled; break;
            case PlayerId.Iina: EnableIina = enabled; break;
            case PlayerId.Vlc: EnableVlc = enabled; break;
            case PlayerId.Infuse: EnableInfuse = enabled; break;
            case PlayerId.Mpv: EnableMpv = enabled; break;
            case PlayerId.NPlayer: EnableNPlayer = enabled; break;
            default: throw new ArgumentOutOfRangeException(nameof(playerId));
        }
    }

    public void PrepareForEditor()
    {
        NormalizeTicketLifetime();
        NormalizeBuiltInPlatformScopes();
        NormalizeCustomPlayers();
        NormalizeUserPlayerPreferences();
    }

    public bool NormalizeTicketLifetime()
    {
        if (TicketLifetimeMinutes >= LaunchTicketStore.MinimumLifetimeMinutes &&
            TicketLifetimeMinutes <= LaunchTicketStore.MaximumLifetimeMinutes)
        {
            return false;
        }

        TicketLifetimeMinutes = LaunchTicketStore.DefaultLifetimeMinutes;
        return true;
    }

    public bool NormalizeCustomPlayers()
    {
        CustomPlayers ??= new CustomPlayerOptionsCollection();
        var changed = CustomPlayers.RemoveAll(custom => custom is null ||
            (!custom.Enabled &&
             string.IsNullOrWhiteSpace(custom.ApplicationName) &&
             string.IsNullOrWhiteSpace(custom.UrlTemplate))) > 0;

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var custom in CustomPlayers)
        {
            custom.ApplicationName ??= string.Empty;
            custom.UrlTemplate ??= string.Empty;
            if (!Guid.TryParseExact(custom.Id, "N", out _) || !ids.Add(custom.Id))
            {
                custom.Id = Guid.NewGuid().ToString("N");
                ids.Add(custom.Id);
                changed = true;
            }
            if (custom.Platforms == PlayerPlatforms.None)
            {
                custom.Platforms = custom.GetEffectivePlatforms();
                changed = true;
            }
            else if ((custom.Platforms & ~PlayerPlatforms.All) != 0)
            {
                custom.Platforms = PlayerPlatforms.All;
                changed = true;
            }
            if (custom.EnablePlaybackReporting &&
                !CustomPlayerTemplate.SupportsHttpRequestHeaders(custom.UrlTemplate))
            {
                custom.EnablePlaybackReporting = false;
                changed = true;
            }
        }

        return changed;
    }

    public bool NormalizeBuiltInPlatformScopes()
    {
        var potPlayer = NormalizeScope(PotPlayerPlatformScope, PlayerPlatforms.Windows);
        var iina = NormalizeScope(IinaPlatformScope, PlayerPlatforms.MacOS);
        var vlc = NormalizeScope(VlcPlatformScope, PlayerPlatforms.All);
        var infuse = NormalizeScope(InfusePlatformScope, PlayerPlatforms.MacOS | PlayerPlatforms.IOS);
        var mpv = NormalizeScope(MpvPlatformScope,
            PlayerPlatforms.Windows | PlayerPlatforms.MacOS | PlayerPlatforms.Linux);
        var nPlayer = NormalizeScope(NPlayerPlatformScope, PlayerPlatforms.IOS | PlayerPlatforms.Android);
        var changed = potPlayer != PotPlayerPlatformScope || iina != IinaPlatformScope ||
            vlc != VlcPlatformScope || infuse != InfusePlatformScope ||
            mpv != MpvPlatformScope || nPlayer != NPlayerPlatformScope;
        PotPlayerPlatformScope = potPlayer;
        IinaPlatformScope = iina;
        VlcPlatformScope = vlc;
        InfusePlatformScope = infuse;
        MpvPlatformScope = mpv;
        NPlayerPlatformScope = nPlayer;
        return changed;
    }

    public PlayerPlatforms GetPlayerPlatforms(PlayerId playerId) => playerId switch
    {
        PlayerId.PotPlayer => PotPlayerPlatformScope,
        PlayerId.Iina => IinaPlatformScope,
        PlayerId.Vlc => VlcPlatformScope,
        PlayerId.Infuse => InfusePlatformScope,
        PlayerId.Mpv => MpvPlatformScope,
        PlayerId.NPlayer => NPlayerPlatformScope,
        _ => PlayerPlatforms.None,
    };

    public void SetPlayerPlatforms(PlayerId playerId, PlayerPlatforms platforms)
    {
        if (platforms == PlayerPlatforms.None || (platforms & ~PlayerPlatforms.All) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(platforms));
        }
        switch (playerId)
        {
            case PlayerId.PotPlayer: PotPlayerPlatformScope = platforms; break;
            case PlayerId.Iina: IinaPlatformScope = platforms; break;
            case PlayerId.Vlc: VlcPlatformScope = platforms; break;
            case PlayerId.Infuse: InfusePlatformScope = platforms; break;
            case PlayerId.Mpv: MpvPlatformScope = platforms; break;
            case PlayerId.NPlayer: NPlayerPlatformScope = platforms; break;
            default: throw new ArgumentOutOfRangeException(nameof(playerId));
        }
    }

    public bool NormalizeUserPlayerPreferences()
    {
        UserPlayerPreferences ??= new UserPlayerPreferenceOptionsCollection();
        var changed = false;
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = UserPlayerPreferences.Count - 1; index >= 0; index--)
        {
            var preference = UserPlayerPreferences[index];
            if (preference is null)
            {
                UserPlayerPreferences.RemoveAt(index);
                changed = true;
                continue;
            }
            var valid = Guid.TryParseExact(preference.UserId, "N", out _) &&
                preference.Platform != ClientPlatform.Unknown &&
                Enum.IsDefined(typeof(ClientPlatform), preference.Platform) &&
                IsStoredPlayerIdValid(preference.PlayerId);
            var key = valid ? preference.UserId + ":" + preference.Platform : string.Empty;
            if (!valid || !keys.Add(key))
            {
                UserPlayerPreferences.RemoveAt(index);
                changed = true;
                continue;
            }

            var normalizedUserId = Guid.ParseExact(preference.UserId, "N").ToString("N");
            if (!string.Equals(preference.UserId, normalizedUserId, StringComparison.Ordinal))
            {
                preference.UserId = normalizedUserId;
                changed = true;
            }
        }
        return changed;
    }

    public string? GetUserDefaultPlayer(Guid userId, ClientPlatform platform)
    {
        if (platform == ClientPlatform.Unknown)
        {
            return null;
        }
        NormalizeUserPlayerPreferences();
        var userIdValue = userId.ToString("N");
        return UserPlayerPreferences.FirstOrDefault(preference =>
            string.Equals(preference.UserId, userIdValue, StringComparison.OrdinalIgnoreCase) &&
            preference.Platform == platform)?.PlayerId;
    }

    public void SetUserDefaultPlayer(Guid userId, ClientPlatform platform, string playerId)
    {
        if (platform == ClientPlatform.Unknown || !Enum.IsDefined(typeof(ClientPlatform), platform))
        {
            throw new ArgumentOutOfRangeException(nameof(platform));
        }
        if (!IsStoredPlayerIdValid(playerId))
        {
            throw new ArgumentException("The player id is invalid.", nameof(playerId));
        }

        NormalizeUserPlayerPreferences();
        var userIdValue = userId.ToString("N");
        UserPlayerPreferences.RemoveAll(preference =>
            string.Equals(preference.UserId, userIdValue, StringComparison.OrdinalIgnoreCase) &&
            preference.Platform == platform);
        UserPlayerPreferences.Add(new UserPlayerPreferenceOptions
        {
            UserId = userIdValue,
            Platform = platform,
            PlayerId = playerId,
        });
    }

    public PlayerId? GetDefaultPlayer(ClientPlatform platform) => platform switch
    {
        ClientPlatform.Windows => DefaultPlayerWindows,
        ClientPlatform.MacOS => DefaultPlayerMacOS,
        ClientPlatform.IOS => DefaultPlayerIOS,
        ClientPlatform.Android => DefaultPlayerAndroid,
        _ => null,
    };

    private void ValidateDefaultPlayer(
        ValidationContext context,
        PlayerId playerId,
        string propertyName,
        ClientPlatform platform)
    {
        if (!IsPlayerEnabled(playerId))
        {
            context.AddValidationError(propertyName, PluginStrings.DefaultPlayerDisabled);
        }
        if (!IncludesPlatform(GetPlayerPlatforms(playerId), platform))
        {
            context.AddValidationError(propertyName, PluginStrings.DefaultPlayerUnsupported);
        }
    }

    public static bool IncludesPlatform(PlayerPlatforms platforms, ClientPlatform platform)
    {
        var flag = platform switch
        {
            ClientPlatform.Windows => PlayerPlatforms.Windows,
            ClientPlatform.MacOS => PlayerPlatforms.MacOS,
            ClientPlatform.IOS => PlayerPlatforms.IOS,
            ClientPlatform.Android => PlayerPlatforms.Android,
            ClientPlatform.Linux => PlayerPlatforms.Linux,
            _ => PlayerPlatforms.None,
        };
        return flag != PlayerPlatforms.None && (platforms & flag) != 0;
    }

    private static PlayerPlatforms NormalizeScope(PlayerPlatforms value, PlayerPlatforms fallback) =>
        value != PlayerPlatforms.None && (value & ~PlayerPlatforms.All) == 0 ? value : fallback;

    private static bool IsStoredPlayerIdValid(string? playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId) || playerId.Length > 40)
        {
            return false;
        }
        if (Enum.TryParse(playerId, true, out PlayerId builtIn) &&
            Enum.IsDefined(typeof(PlayerId), builtIn))
        {
            return true;
        }
        const string prefix = "custom-";
        return playerId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParseExact(playerId.Substring(prefix.Length), "N", out _);
    }
}
