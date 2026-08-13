using System;
using System.IO;
using System.Text;

namespace Emby.ExternalPlayer.Services;

public static class SafeFileNamePolicy
{
    private const int MaximumLength = 120;

    public static string CreateUrlTitle(string? title) => Sanitize(title, "media");

    public static string CreateFileName(string? path, string extension, string fallbackBaseName)
    {
        string? baseName;
        try
        {
            baseName = Path.GetFileNameWithoutExtension(path);
        }
        catch (Exception exception) when (
            exception is ArgumentException ||
            exception is NotSupportedException ||
            exception is PathTooLongException)
        {
            baseName = null;
        }
        return Sanitize(baseName, fallbackBaseName) + "." +
            ServerUrlBuilder.NormalizeExtension(extension, "bin");
    }

    private static string Sanitize(string? title, string fallback)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return fallback;
        }
        var normalized = title.Normalize(NormalizationForm.FormC);
        var builder = new StringBuilder(Math.Min(normalized.Length, MaximumLength));
        for (var index = 0; index < normalized.Length && builder.Length < MaximumLength; index++)
        {
            var character = normalized[index];
            if (char.IsHighSurrogate(character) &&
                index + 1 < normalized.Length &&
                char.IsLowSurrogate(normalized[index + 1]))
            {
                if (builder.Length + 2 > MaximumLength)
                {
                    break;
                }
                builder.Append(character);
                builder.Append(normalized[++index]);
                continue;
            }

            if (char.IsControl(character) || char.IsSurrogate(character) ||
                character == '/' || character == '\\' || character == '?' || character == '#' ||
                character == '"')
            {
                builder.Append('_');
            }
            else
            {
                builder.Append(character);
            }
        }

        var result = builder.ToString().Trim(' ', '.');
        return string.IsNullOrWhiteSpace(result) ||
               result.Trim(' ', '.', '_', '-').Length == 0
            ? fallback
            : result;
    }

    public static string CreateGeneric(string extension) =>
        "media." + ServerUrlBuilder.NormalizeExtension(extension, "bin");

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

        var ascii = new StringBuilder(safeFileName.Length);
        var requiresExtendedName = false;
        foreach (var character in safeFileName)
        {
            if (character >= 0x20 && character <= 0x7e)
            {
                ascii.Append(character);
            }
            else
            {
                ascii.Append('_');
                requiresExtendedName = true;
            }
        }
        var disposition = "inline; filename=\"" + ascii + "\"";
        return requiresExtendedName
            ? disposition + "; filename*=UTF-8''" + Uri.EscapeDataString(safeFileName)
            : disposition;
    }
}
