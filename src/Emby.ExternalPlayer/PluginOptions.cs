using System;
using System.ComponentModel;
using Emby.ExternalPlayer.Domain;
using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Validation;

namespace Emby.ExternalPlayer;

public enum ButtonPlacement
{
    AfterPrimaryPlay,
    EndOfActionRow,
}

public enum StreamMode
{
    SecureTicketRelay,
    LegacyTokenUrl,
}

public enum PlayerId
{
    PotPlayer,
    Iina,
    Vlc,
    Infuse,
    Mpv,
    NPlayer,
}

public sealed class PluginOptions : EditableOptionsBase
{
    public override string EditorTitle => "External Player";

    public override string EditorDescription =>
        "Adds a lightweight external-player chooser to Emby Web. " +
        "The default secure relay keeps the Emby access token out of player URLs.";

    [Description("Master switch for the plugin API and Web integration.")]
    public bool Enabled { get; set; } = true;

    [Description("Show the external-player button on supported Emby Web detail pages.")]
    public bool EnableWebButton { get; set; } = true;

    [DisplayName("Button text")]
    [Description("Text displayed beside Emby's native play action.")]
    public string ButtonText { get; set; } = "外部播放";

    public ButtonPlacement ButtonPlacement { get; set; } = ButtonPlacement.AfterPrimaryPlay;

    public bool ShowOnlyPlatformPlayers { get; set; } = true;

    public bool ResumeByDefault { get; set; } = true;

    [DisplayName("Restart near end (minutes)")]
    [Description("Treat a saved position this close to the end as completed. Allowed range: 0 to 30 minutes.")]
    public int RestartNearEndMinutes { get; set; } = 5;

    [Description("SecureTicketRelay hides Emby tokens and supports local files. LegacyTokenUrl exposes the Emby token in the player URL and should only be used for incompatible remote sources.")]
    public StreamMode StreamMode { get; set; } = StreamMode.SecureTicketRelay;

    [DisplayName("Ticket lifetime (minutes)")]
    [Description("Absolute lifetime of a playback ticket. Allowed range: 30 to 720 minutes.")]
    public int TicketLifetimeMinutes { get; set; } = 480;

    public bool EnablePotPlayer { get; set; } = true;

    public bool EnableIina { get; set; } = true;

    public bool EnableVlc { get; set; } = true;

    public bool EnableInfuse { get; set; } = true;

    public bool EnableMpv { get; set; }

    public bool EnableNPlayer { get; set; }

    public PlayerId DefaultPlayerWindows { get; set; } = PlayerId.PotPlayer;

    public PlayerId DefaultPlayerMacOS { get; set; } = PlayerId.Iina;

    public PlayerId DefaultPlayerIOS { get; set; } = PlayerId.Infuse;

    public PlayerId DefaultPlayerAndroid { get; set; } = PlayerId.Vlc;

    [Description("Writes diagnostic event names only. Tokens and resolved URLs are never logged.")]
    public bool DebugLogging { get; set; }

    protected override void Validate(ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(ButtonText))
        {
            context.AddValidationError(nameof(ButtonText), "Button text cannot be empty.");
        }

        if (ButtonText?.Length > 40)
        {
            context.AddValidationError(nameof(ButtonText), "Button text cannot exceed 40 characters.");
        }

        if (TicketLifetimeMinutes < 30 || TicketLifetimeMinutes > 720)
        {
            context.AddValidationError(
                nameof(TicketLifetimeMinutes),
                "Ticket lifetime must be between 30 and 720 minutes.");
        }

        if (RestartNearEndMinutes < 0 || RestartNearEndMinutes > 30)
        {
            context.AddValidationError(
                nameof(RestartNearEndMinutes),
                "Restart-near-end must be between 0 and 30 minutes.");
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
            context.AddValidationError(propertyName, "The selected default player is disabled.");
        }

        if (Array.IndexOf(supportedPlayers, playerId) < 0)
        {
            context.AddValidationError(propertyName, "The selected default player does not support this platform.");
        }
    }
}
