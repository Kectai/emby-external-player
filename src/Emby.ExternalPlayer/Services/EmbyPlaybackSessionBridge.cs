using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Emby.ExternalPlayer.Domain;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Session;

namespace Emby.ExternalPlayer.Services;

public sealed class EmbyPlaybackSessionBridge : IPlaybackSessionBridge
{
    private readonly IUserManager userManager;
    private readonly ILibraryManager libraryManager;
    private readonly IMediaSourceManager mediaSourceManager;
    private readonly ISessionManager sessionManager;

    public EmbyPlaybackSessionBridge(
        IUserManager userManager,
        ILibraryManager libraryManager,
        IMediaSourceManager mediaSourceManager,
        ISessionManager sessionManager)
    {
        this.userManager = userManager;
        this.libraryManager = libraryManager;
        this.mediaSourceManager = mediaSourceManager;
        this.sessionManager = sessionManager;
    }

    public async Task<PlaybackSessionHandle> StartAsync(
        PlaybackReportGrant grant,
        string launchId,
        PlaybackReportRequest request)
    {
        var context = RequireAuthorized(grant);
        var reportedDeviceId = "external-player-" + launchId.Substring(0, 12);
        var session = sessionManager.LogSessionActivity(
            grant.PlayerName,
            request.ProtocolVersion.ToString(CultureInfo.InvariantCulture),
            reportedDeviceId,
            grant.PlayerName,
            grant.ClientAddress ?? string.Empty,
            context.User);
        var playSessionId = Guid.NewGuid().ToString("N");
        var handle = new PlaybackSessionHandle
        {
            SessionId = session.Id,
            PlaySessionId = playSessionId,
            NativeSession = session,
        };
        try
        {
            await sessionManager.OnPlaybackStart(new PlaybackStartInfo
            {
                ItemId = grant.ItemId.ToString("N"),
                MediaSourceId = grant.MediaSourceId,
                PlaySessionId = playSessionId,
                SessionId = session.Id,
                PositionTicks = request.PositionTicks,
                RunTimeTicks = grant.RunTimeTicks > 0 ? grant.RunTimeTicks : null,
                CanSeek = true,
                IsPaused = request.IsPaused,
                PlaybackRate = request.PlaybackRate,
                PlayMethod = PlayMethod.DirectPlay,
                EventName = ProgressEvent.TimeUpdate,
            }, session).ConfigureAwait(false);
            return handle;
        }
        catch
        {
            sessionManager.ReportSessionEnded(session.Id);
            throw;
        }
    }

    public async Task ProgressAsync(
        PlaybackReportGrant grant,
        PlaybackSessionHandle session,
        PlaybackReportRequest request,
        PlaybackProgressEventKind eventKind)
    {
        RequireAuthorized(grant);
        var sessionInfo = RequireSession(session);
        await sessionManager.OnPlaybackProgress(new PlaybackProgressInfo
        {
            ItemId = grant.ItemId.ToString("N"),
            MediaSourceId = grant.MediaSourceId,
            PlaySessionId = session.PlaySessionId,
            SessionId = session.SessionId,
            PositionTicks = request.PositionTicks,
            RunTimeTicks = grant.RunTimeTicks > 0 ? grant.RunTimeTicks : null,
            CanSeek = true,
            IsPaused = request.IsPaused,
            PlaybackRate = request.PlaybackRate,
            PlayMethod = PlayMethod.DirectPlay,
            EventName = eventKind switch
            {
                PlaybackProgressEventKind.Pause => ProgressEvent.Pause,
                PlaybackProgressEventKind.Unpause => ProgressEvent.Unpause,
                _ => ProgressEvent.TimeUpdate,
            },
        }, sessionInfo).ConfigureAwait(false);
    }

    public async Task StopAsync(
        PlaybackReportGrant grant,
        PlaybackSessionHandle session,
        long positionTicks,
        bool isAutomated)
    {
        // Client-requested Stop still revalidates access. Automated Stop is an
        // internal cleanup path and must be able to end an existing play
        // session after the user's or media source's authorization is revoked.
        if (!isAutomated)
        {
            RequireAuthorized(grant);
        }
        var sessionInfo = RequireSession(session);
        await sessionManager.OnPlaybackStopped(new PlaybackStopInfo
        {
            ItemId = grant.ItemId.ToString("N"),
            MediaSourceId = grant.MediaSourceId,
            PlaySessionId = session.PlaySessionId,
            SessionId = session.SessionId,
            PositionTicks = positionTicks,
            Failed = false,
            IsAutomated = isAutomated,
        }, sessionInfo).ConfigureAwait(false);
        // OnPlaybackStopped removes the play session and raises asynchronous
        // events whose handlers may still read SessionInfo after this Task has
        // completed. Ending the device session here disposes that object too
        // early; Emby will reap the now-idle synthetic session itself.
    }

    private (User User, BaseItem Item) RequireAuthorized(PlaybackReportGrant grant)
    {
        var user = userManager.GetUserById(grant.UserId);
        var item = libraryManager.GetItemById(grant.ItemId);
        if (user is null || item is null || !item.IsVisible(user) ||
            !user.Policy.EnableMediaPlayback)
        {
            throw new PlaybackAuthorizationException(
                "The playback reporting grant is no longer authorized.");
        }
        var source = mediaSourceManager.GetStaticMediaSources(
                item,
                enablePathSubstitution: false,
                fillChapters: false,
                deviceProfile: null,
                user: user)
            .FirstOrDefault(candidate => string.Equals(
                candidate.Id,
                grant.MediaSourceId,
                StringComparison.Ordinal));
        if (source is null || source.Protocol != MediaProtocol.File)
        {
            throw new PlaybackAuthorizationException(
                "The playback reporting media source is no longer authorized.");
        }
        return (user, item);
    }

    private static SessionInfo RequireSession(PlaybackSessionHandle handle)
    {
        if (handle.NativeSession is not SessionInfo session)
        {
            throw new PlaybackAuthorizationException("The Emby playback session is unavailable.");
        }
        return session;
    }
}
