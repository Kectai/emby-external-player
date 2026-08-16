using System;
using System.Threading.Tasks;
using Emby.ExternalPlayer.Domain;

namespace Emby.ExternalPlayer.Services;

public interface IPlaybackSessionBridge
{
    Task<PlaybackSessionHandle> StartAsync(
        PlaybackReportGrant grant,
        string launchId,
        PlaybackReportRequest request);

    Task ProgressAsync(
        PlaybackReportGrant grant,
        PlaybackSessionHandle session,
        PlaybackReportRequest request,
        PlaybackProgressEventKind eventKind);

    Task StopAsync(
        PlaybackReportGrant grant,
        PlaybackSessionHandle session,
        long positionTicks,
        bool isAutomated);
}

public sealed class PlaybackAuthorizationException : Exception
{
    public PlaybackAuthorizationException(string message)
        : base(message)
    {
    }
}
