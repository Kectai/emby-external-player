using System;
using System.IO;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.MediaInfo;

namespace Emby.ExternalPlayer.Services;

public sealed class RemoteStrmSource
{
    public RemoteStrmSource(string url, FileInfo descriptorFile)
    {
        Url = url;
        DescriptorFile = descriptorFile;
    }

    public string Url { get; }

    public FileInfo DescriptorFile { get; }
}

public static class RemoteMediaSourcePolicy
{
    public const int MaximumUrlLength = 16384;

    private const long MaximumStrmFileLength = MaximumUrlLength + 4L;

    public static void RequireAuthorizedPlaybackSource(
        string? itemPath,
        MediaSourceInfo source,
        bool isRemoteStrm)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }
        if (isRemoteStrm)
        {
            RequireDirectStrmSource(itemPath, source);
            return;
        }
        if (source.Protocol != MediaProtocol.File)
        {
            throw new ArgumentException(
                "The selected media source is no longer a local file source.",
                nameof(source));
        }
    }

    public static RemoteStrmSource RequireDirectStrmSource(
        string? itemPath,
        MediaSourceInfo source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        FileInfo descriptor;
        try
        {
            descriptor = LocalMediaFilePolicy.RequireExistingFile(itemPath);
        }
        catch (InvalidOperationException exception)
        {
            throw new ArgumentException(
                "The selected remote media item is not backed by a local STRM file.",
                nameof(itemPath),
                exception);
        }

        if (!string.Equals(descriptor.Extension, ".strm", StringComparison.OrdinalIgnoreCase) ||
            descriptor.Length <= 0 || descriptor.Length > MaximumStrmFileLength)
        {
            throw new ArgumentException(
                "The selected remote media item is not a supported STRM descriptor.",
                nameof(itemPath));
        }

        if (source.Protocol != MediaProtocol.Http || source.RequiresOpening ||
            !string.IsNullOrWhiteSpace(source.OpenToken) ||
            (source.RequiredHttpHeaders is not null && source.RequiredHttpHeaders.Count > 0))
        {
            throw new ArgumentException(
                "Emby-managed remote media sources are not supported for external playback.",
                nameof(source));
        }

        string descriptorUrl;
        try
        {
            descriptorUrl = ReadSingleUrl(descriptor);
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException)
        {
            throw new ArgumentException(
                "The STRM descriptor could not be read safely.",
                nameof(itemPath),
                exception);
        }
        if (!TryCanonicalizeHttpUrl(source.Path, out var mediaSourceUrl) ||
            !string.Equals(descriptorUrl, mediaSourceUrl, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The selected media source does not match its STRM descriptor.",
                nameof(source));
        }

        return new RemoteStrmSource(mediaSourceUrl!, descriptor);
    }

    public static bool TryCanonicalizeHttpUrl(string? value, out string? streamUrl)
    {
        streamUrl = null;
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumUrlLength ||
            !Uri.TryCreate(value, UriKind.Absolute, out var candidate) ||
            (!string.Equals(candidate.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(candidate.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) ||
            !string.IsNullOrEmpty(candidate.UserInfo) || !string.IsNullOrEmpty(candidate.Fragment))
        {
            return false;
        }

        var requiresEscaping = false;
        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                return false;
            }
            if (character < 0x21 || character > 0x7e)
            {
                requiresEscaping = true;
            }
        }

        // Preserve already escaped ASCII URLs byte-for-byte. Signed URLs can
        // depend on escaping, percent-hex casing and query ordering.
        streamUrl = requiresEscaping || !candidate.IsWellFormedOriginalString()
            ? candidate.AbsoluteUri
            : value;
        return true;
    }

    private static string ReadSingleUrl(FileInfo descriptor)
    {
        string value;
        using (var reader = new StreamReader(
                   descriptor.FullName,
                   detectEncodingFromByteOrderMarks: true))
        {
            // FileInfo was checked above, but a trusted-library file can still
            // change between stat and read. Keep the allocation bounded anyway.
            var buffer = new char[MaximumUrlLength + 5];
            var length = reader.ReadBlock(buffer, 0, buffer.Length);
            if (length == buffer.Length || reader.Read() >= 0)
            {
                throw new ArgumentException(
                    "The STRM descriptor URL is too long.",
                    nameof(descriptor));
            }
            value = new string(buffer, 0, length);
        }

        value = value.Trim('\uFEFF', ' ', '\t', '\r', '\n');
        if (value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0 ||
            !TryCanonicalizeHttpUrl(value, out var canonical))
        {
            throw new ArgumentException(
                "The STRM descriptor must contain one supported absolute HTTP(S) URL.",
                nameof(descriptor));
        }

        return canonical!;
    }
}
