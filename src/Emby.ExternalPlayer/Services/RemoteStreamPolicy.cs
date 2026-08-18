using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Emby.ExternalPlayer.Domain;

namespace Emby.ExternalPlayer.Services;

public static class RemoteStreamPolicy
{
    private const string LaunchTokenVersion = "v1";
    private static readonly Regex TicketPattern = new(
        "^[A-Za-z0-9_-]{43}$",
        RegexOptions.CultureInvariant);
    private static readonly HashSet<string> MediaExtensions = new(
        new[]
        {
            "3g2", "3gp", "aac", "ac3", "alac", "ape", "asf", "avi", "divx",
            "dts", "eac3", "flac", "flv", "m2ts", "m2v", "m3u8", "m4a", "m4v",
            "mka", "mkv", "mov", "mp2", "mp3", "mp4", "mpeg", "mpg", "mts",
            "oga", "ogg", "ogm", "ogv", "opus", "rm", "rmvb", "ts", "vob",
            "wav", "webm", "wma", "wmv",
        },
        StringComparer.OrdinalIgnoreCase);

    public static string ResolveMediaExtension(string? container, string remoteUrl)
    {
        if (Uri.TryCreate(remoteUrl, UriKind.Absolute, out var remoteUri))
        {
            var pathExtension = Path.GetExtension(remoteUri.AbsolutePath).TrimStart('.');
            var normalizedPathExtension = ServerUrlBuilder.NormalizeExtension(pathExtension, string.Empty);
            if (MediaExtensions.Contains(normalizedPathExtension))
            {
                return normalizedPathExtension;
            }
        }

        var normalizedContainer = ServerUrlBuilder.NormalizeExtension(container, string.Empty);
        return MediaExtensions.Contains(normalizedContainer)
            ? normalizedContainer
            : "mkv";
    }

    public static string CreateFileName(string? title, string? extension)
    {
        var safeExtension = ServerUrlBuilder.NormalizeExtension(extension, "mkv");
        return SafeFileNamePolicy.CreateUrlTitle(title) + "." + safeExtension;
    }

    public static string CreateLaunchToken(
        string mediaTicket,
        PlaybackReportTicket progressTicket)
    {
        if (!IsValidTicket(mediaTicket))
        {
            throw new ArgumentException("The remote media ticket is invalid.", nameof(mediaTicket));
        }
        if (progressTicket is null || !IsValidTicket(progressTicket.Value))
        {
            throw new ArgumentException("The playback reporting ticket is invalid.", nameof(progressTicket));
        }

        return LaunchTokenVersion + "." + mediaTicket + "." + progressTicket.Value + "." +
            progressTicket.ExpiresAtUtc.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
    }

    public static bool TryParseLaunchToken(
        string? value,
        out RemoteLaunchCredentials credentials)
    {
        credentials = default;
        if (IsValidTicket(value))
        {
            credentials = new RemoteLaunchCredentials(value!, null, null);
            return true;
        }
        if (string.IsNullOrEmpty(value) || value.Length > 160)
        {
            return false;
        }

        var parts = value.Split('.');
        if (parts.Length != 4 || parts[0] != LaunchTokenVersion ||
            !IsValidTicket(parts[1]) || !IsValidTicket(parts[2]) ||
            !long.TryParse(
                parts[3],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var expiresAtUnixSeconds))
        {
            return false;
        }

        try
        {
            credentials = new RemoteLaunchCredentials(
                parts[1],
                parts[2],
                DateTimeOffset.FromUnixTimeSeconds(expiresAtUnixSeconds));
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static bool IsValidTicket(string? value) =>
        !string.IsNullOrEmpty(value) && TicketPattern.IsMatch(value);
}

public readonly struct RemoteLaunchCredentials
{
    public RemoteLaunchCredentials(
        string mediaTicket,
        string? progressTicket,
        DateTimeOffset? progressExpiresAtUtc)
    {
        MediaTicket = mediaTicket;
        ProgressTicket = progressTicket;
        ProgressExpiresAtUtc = progressExpiresAtUtc;
    }

    public string MediaTicket { get; }

    public string? ProgressTicket { get; }

    public DateTimeOffset? ProgressExpiresAtUtc { get; }

    public bool HasPlaybackReporting => !string.IsNullOrEmpty(ProgressTicket);
}
