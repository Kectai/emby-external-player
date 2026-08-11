using System;
using System.IO;
using System.Text;

namespace Emby.ExternalPlayer.Services;

public static class SafeFileNamePolicy
{
    private const int MaximumLength = 120;

    public static string Create(string? path, string fallbackExtension)
    {
        var fallback = "media." + ServerUrlBuilder.NormalizeExtension(fallbackExtension, "bin");
        if (string.IsNullOrWhiteSpace(path))
        {
            return fallback;
        }

        var raw = Path.GetFileName(path);
        var builder = new StringBuilder(Math.Min(raw.Length, MaximumLength));
        foreach (var character in raw)
        {
            if (builder.Length >= MaximumLength)
            {
                break;
            }

            if ((character >= 'a' && character <= 'z') ||
                (character >= 'A' && character <= 'Z') ||
                (character >= '0' && character <= '9') ||
                character == '.' || character == '-' || character == '_' || character == ' ')
            {
                builder.Append(character);
            }
            else
            {
                builder.Append('_');
            }
        }

        var result = builder.ToString().Trim(' ', '.');
        return string.IsNullOrWhiteSpace(result) ? fallback : result;
    }

    public static string CreateContentDisposition(string safeFileName)
    {
        if (string.IsNullOrWhiteSpace(safeFileName) ||
            safeFileName.IndexOf('\r') >= 0 ||
            safeFileName.IndexOf('\n') >= 0 ||
            safeFileName.IndexOf('"') >= 0 ||
            safeFileName.IndexOf('\\') >= 0)
        {
            throw new InvalidOperationException("The response filename is invalid.");
        }

        return "inline; filename=\"" + safeFileName + "\"";
    }
}
