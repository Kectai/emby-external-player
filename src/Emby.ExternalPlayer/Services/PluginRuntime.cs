namespace Emby.ExternalPlayer.Services;

public sealed class PluginRuntime
{
    public PluginRuntime()
    {
        Clock = new SystemClock();
        Tickets = new LaunchTicketStore(Clock);
        PlaybackReportTickets = new PlaybackReportTicketStore(Clock);
        PlaybackReports = new PlaybackReportCoordinator(PlaybackReportTickets, Clock);
        RemoteStreams = new RemoteRedirectResolver();
        Players = new PlayerAdapterRegistry();
    }

    public IClock Clock { get; }

    public LaunchTicketStore Tickets { get; }

    public PlaybackReportTicketStore PlaybackReportTickets { get; }

    public PlaybackReportCoordinator PlaybackReports { get; }

    public RemoteRedirectResolver RemoteStreams { get; }

    public PlayerAdapterRegistry Players { get; }
}
