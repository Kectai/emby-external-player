using System;

namespace Emby.ExternalPlayer.Services;

public static class ResumePositionPolicy
{
    public static readonly long MinimumPositionTicks = TimeSpan.FromSeconds(10).Ticks;

    public static long Normalize(long positionTicks, long? runtimeTicks, int restartNearEndMinutes)
    {
        if (positionTicks < MinimumPositionTicks)
        {
            return 0;
        }

        if (restartNearEndMinutes < 0 || restartNearEndMinutes > 30)
        {
            throw new ArgumentOutOfRangeException(nameof(restartNearEndMinutes));
        }

        if (runtimeTicks.HasValue && runtimeTicks.Value > 0)
        {
            var remainingTicks = runtimeTicks.Value - positionTicks;
            if (remainingTicks <= TimeSpan.FromMinutes(restartNearEndMinutes).Ticks)
            {
                return 0;
            }
        }

        return positionTicks;
    }
}
