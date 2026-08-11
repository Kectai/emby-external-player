using System;
using System.IO;

namespace Emby.ExternalPlayer.Services;

public static class LocalMediaFilePolicy
{
    public static FileInfo RequireExistingFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
        {
            throw new InvalidOperationException("The selected media source is not a local file.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception exception) when (
            exception is ArgumentException ||
            exception is NotSupportedException ||
            exception is PathTooLongException)
        {
            throw new InvalidOperationException("The selected media source path is invalid.", exception);
        }

        var file = new FileInfo(fullPath);
        if (!file.Exists)
        {
            throw new InvalidOperationException("The selected media source file is unavailable.");
        }

        return file;
    }
}
