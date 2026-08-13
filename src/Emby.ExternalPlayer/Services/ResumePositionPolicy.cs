namespace Emby.ExternalPlayer.Services;

public static class ResumePositionPolicy
{
    public static long FromEmbyUserData(long positionTicks) =>
        positionTicks > 0 ? positionTicks : 0;
}
