using System;
using Emby.ExternalPlayer.Domain;
using Emby.ExternalPlayer.Localization;
using Emby.ExternalPlayer.Services;
using Emby.Web.GenericEdit;
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

public enum StreamMode
{
    [DescriptionL(nameof(PluginStrings.SecureTicketRelay), typeof(PluginStrings))]
    SecureTicketRelay,

    [DescriptionL(nameof(PluginStrings.LegacyTokenUrl), typeof(PluginStrings))]
    LegacyTokenUrl,
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
    public string ButtonText { get; set; } = "外部播放";

    [DisplayNameL(nameof(PluginStrings.ButtonPlacement), typeof(PluginStrings))]
    public ButtonPlacement ButtonPlacement { get; set; } = ButtonPlacement.AfterPrimaryPlay;

    [DisplayNameL(nameof(PluginStrings.ShowOnlyPlatformPlayers), typeof(PluginStrings))]
    public bool ShowOnlyPlatformPlayers { get; set; } = true;

    [DisplayNameL(nameof(PluginStrings.ResumeByDefault), typeof(PluginStrings))]
    public bool ResumeByDefault { get; set; } = true;

    [DisplayNameL(nameof(PluginStrings.RestartNearEndMinutes), typeof(PluginStrings))]
    [DescriptionL(nameof(PluginStrings.RestartNearEndMinutesDescription), typeof(PluginStrings))]
    public int RestartNearEndMinutes { get; set; } = 5;

    [DisplayNameL(nameof(PluginStrings.StreamMode), typeof(PluginStrings))]
    [DescriptionL(nameof(PluginStrings.StreamModeDescription), typeof(PluginStrings))]
    public StreamMode StreamMode { get; set; } = StreamMode.SecureTicketRelay;

    [DisplayNameL(nameof(PluginStrings.TicketLifetimeMinutes), typeof(PluginStrings))]
    [DescriptionL(nameof(PluginStrings.TicketLifetimeMinutesDescription), typeof(PluginStrings))]
    public int TicketLifetimeMinutes { get; set; } = 480;

    [DisplayNameL(nameof(PluginStrings.EnablePotPlayer), typeof(PluginStrings))]
    public bool EnablePotPlayer { get; set; } = true;

    [DisplayNameL(nameof(PluginStrings.EnableIINA), typeof(PluginStrings))]
    public bool EnableIina { get; set; } = true;

    [DisplayNameL(nameof(PluginStrings.EnableVLC), typeof(PluginStrings))]
    public bool EnableVlc { get; set; } = true;

    [DisplayNameL(nameof(PluginStrings.EnableInfuse), typeof(PluginStrings))]
    public bool EnableInfuse { get; set; } = true;

    [DisplayNameL(nameof(PluginStrings.EnableMpv), typeof(PluginStrings))]
    public bool EnableMpv { get; set; }

    [DisplayNameL(nameof(PluginStrings.EnableNPlayer), typeof(PluginStrings))]
    public bool EnableNPlayer { get; set; }

    [DisplayNameL(nameof(PluginStrings.DefaultPlayerWindows), typeof(PluginStrings))]
    public PlayerId DefaultPlayerWindows { get; set; } = PlayerId.PotPlayer;

    [DisplayNameL(nameof(PluginStrings.DefaultPlayerMacOS), typeof(PluginStrings))]
    public PlayerId DefaultPlayerMacOS { get; set; } = PlayerId.Iina;

    [DisplayNameL(nameof(PluginStrings.DefaultPlayerIOS), typeof(PluginStrings))]
    public PlayerId DefaultPlayerIOS { get; set; } = PlayerId.Infuse;

    [DisplayNameL(nameof(PluginStrings.DefaultPlayerAndroid), typeof(PluginStrings))]
    public PlayerId DefaultPlayerAndroid { get; set; } = PlayerId.Vlc;

    [DisplayNameL(nameof(PluginStrings.CustomPlayers), typeof(PluginStrings))]
    [DescriptionL(nameof(PluginStrings.CustomPlayersDescription), typeof(PluginStrings))]
    public CustomPlayerOptionsCollection CustomPlayers { get; set; } = new()
    {
        new CustomPlayerOptions(),
        new CustomPlayerOptions(),
        new CustomPlayerOptions(),
    };

    [DisplayNameL(nameof(PluginStrings.DebugLogging), typeof(PluginStrings))]
    [DescriptionL(nameof(PluginStrings.DebugLoggingDescription), typeof(PluginStrings))]
    public bool DebugLogging { get; set; }

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

        if (RestartNearEndMinutes < 0 || RestartNearEndMinutes > 30)
        {
            context.AddValidationError(
                nameof(RestartNearEndMinutes),
                PluginStrings.RestartNearEndInvalid);
        }

        ValidateDefaultPlayer(
            context,
            DefaultPlayerWindows,
            nameof(DefaultPlayerWindows),
            PlayerId.PotPlayer, PlayerId.Vlc, PlayerId.Mpv);
        ValidateDefaultPlayer(
            context,
            DefaultPlayerMacOS,
            nameof(DefaultPlayerMacOS),
            PlayerId.Iina, PlayerId.Vlc, PlayerId.Infuse, PlayerId.Mpv);
        ValidateDefaultPlayer(
            context,
            DefaultPlayerIOS,
            nameof(DefaultPlayerIOS),
            PlayerId.Infuse, PlayerId.Vlc, PlayerId.NPlayer);
        ValidateDefaultPlayer(
            context,
            DefaultPlayerAndroid,
            nameof(DefaultPlayerAndroid),
            PlayerId.Vlc, PlayerId.NPlayer);

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

    public PlayerId? GetDefaultPlayer(ClientPlatform platform)
    {
        return platform switch
        {
            ClientPlatform.Windows => DefaultPlayerWindows,
            ClientPlatform.MacOS => DefaultPlayerMacOS,
            ClientPlatform.IOS => DefaultPlayerIOS,
            ClientPlatform.Android => DefaultPlayerAndroid,
            _ => null,
        };
    }

    private void ValidateDefaultPlayer(
        ValidationContext context,
        PlayerId playerId,
        string propertyName,
        params PlayerId[] supportedPlayers)
    {
        if (!IsPlayerEnabled(playerId))
        {
            context.AddValidationError(propertyName, PluginStrings.DefaultPlayerDisabled);
        }

        if (Array.IndexOf(supportedPlayers, playerId) < 0)
        {
            context.AddValidationError(propertyName, PluginStrings.DefaultPlayerUnsupported);
        }
    }
}
