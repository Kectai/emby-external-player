namespace Emby.ExternalPlayer.Services;

public sealed class PluginRuntime
{
    public PluginRuntime()
    {
        Clock = new SystemClock();
        Tickets = new LaunchTicketStore(Clock);
        Players = new PlayerAdapterRegistry();
    }

    public IClock Clock { get; }

    public LaunchTicketStore Tickets { get; }

    public PlayerAdapterRegistry Players { get; }
}
