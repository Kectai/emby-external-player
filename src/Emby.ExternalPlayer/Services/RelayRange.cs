using System;
using System.Globalization;

namespace Emby.ExternalPlayer.Services;

public static class RelayRange
{
    public static string BuildHeader(long offset, long length)
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        var end = checked(offset + length - 1);
        return "bytes=" + offset.ToString(CultureInfo.InvariantCulture) + "-" +
               end.ToString(CultureInfo.InvariantCulture);
    }
}
