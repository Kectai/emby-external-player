using System.Text;
using Emby.ExternalPlayer.Services;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.MediaInfo;

namespace Emby.ExternalPlayer.Tests;

[TestClass]
public sealed class RemoteMediaSourcePolicyTests
{
    [TestMethod]
    public void RequireDirectStrmSource_AcceptsMatchingStaticHttpSource()
    {
        const string url =
            "https://openlist.example/d/movie.mkv?sign=a%2fb&expires=0&part=a+b";

        WithStrm(url, path =>
        {
            var result = RemoteMediaSourcePolicy.RequireDirectStrmSource(
                path,
                CreateSource(url));

            Assert.AreEqual(url, result.Url);
            Assert.AreEqual(Path.GetFullPath(path), result.DescriptorFile.FullName);
        });
    }

    [TestMethod]
    public void RequireAuthorizedPlaybackSource_AcceptsTheIssuedSourceKindOnly()
    {
        const string url = "https://openlist.example/d/movie.mkv?sign=long-lived";
        WithStrm(url, path =>
        {
            var remote = CreateSource(url);
            RemoteMediaSourcePolicy.RequireAuthorizedPlaybackSource(
                path,
                remote,
                isRemoteStrm: true);
            Assert.ThrowsExactly<ArgumentException>(() =>
                RemoteMediaSourcePolicy.RequireAuthorizedPlaybackSource(
                    path,
                    remote,
                    isRemoteStrm: false));

            var local = new MediaSourceInfo
            {
                Protocol = MediaProtocol.File,
                Path = path,
            };
            RemoteMediaSourcePolicy.RequireAuthorizedPlaybackSource(
                path,
                local,
                isRemoteStrm: false);
            Assert.ThrowsExactly<ArgumentException>(() =>
                RemoteMediaSourcePolicy.RequireAuthorizedPlaybackSource(
                    path,
                    local,
                    isRemoteStrm: true));
        });
    }

    [TestMethod]
    public void RequireDirectStrmSource_RejectsNonStrmAndMismatchedDescriptor()
    {
        WithFile("movie.txt", "https://openlist.example/d/movie.mkv", path =>
            Assert.ThrowsExactly<ArgumentException>(() =>
                RemoteMediaSourcePolicy.RequireDirectStrmSource(
                    path,
                    CreateSource("https://openlist.example/d/movie.mkv"))));

        WithStrm("https://openlist.example/d/first.mkv", path =>
            Assert.ThrowsExactly<ArgumentException>(() =>
                RemoteMediaSourcePolicy.RequireDirectStrmSource(
                    path,
                    CreateSource("https://openlist.example/d/second.mkv"))));
    }

    [TestMethod]
    public void RequireDirectStrmSource_RejectsManagedOrDynamicMediaSources()
    {
        const string url = "https://openlist.example/d/movie.mkv?sign=long-lived";
        WithStrm(url, path =>
        {
            var requiresOpening = CreateSource(url);
            requiresOpening.RequiresOpening = true;
            Assert.ThrowsExactly<ArgumentException>(() =>
                RemoteMediaSourcePolicy.RequireDirectStrmSource(path, requiresOpening));

            var openToken = CreateSource(url);
            openToken.OpenToken = "provider-token";
            Assert.ThrowsExactly<ArgumentException>(() =>
                RemoteMediaSourcePolicy.RequireDirectStrmSource(path, openToken));

            var headers = CreateSource(url);
            headers.RequiredHttpHeaders = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer provider-token",
            };
            Assert.ThrowsExactly<ArgumentException>(() =>
                RemoteMediaSourcePolicy.RequireDirectStrmSource(path, headers));

            var unsupportedProtocol = CreateSource(url);
            unsupportedProtocol.Protocol = MediaProtocol.Rtsp;
            Assert.ThrowsExactly<ArgumentException>(() =>
                RemoteMediaSourcePolicy.RequireDirectStrmSource(path, unsupportedProtocol));
        });
    }

    [TestMethod]
    [DataRow("https://openlist.example/first\nhttps://openlist.example/second")]
    [DataRow("file:///etc/passwd")]
    [DataRow("https://openlist.example/d/movie.mkv#fragment")]
    public void RequireDirectStrmSource_RejectsUnsafeDescriptor(string descriptor)
    {
        WithStrm(descriptor, path =>
            Assert.ThrowsExactly<ArgumentException>(() =>
                RemoteMediaSourcePolicy.RequireDirectStrmSource(
                    path,
                    CreateSource(descriptor))));
    }

    [TestMethod]
    public void RequireDirectStrmSource_RejectsEmbeddedUrlCredentials()
    {
        var descriptor = string.Concat(
            "https",
            "://user",
            ':',
            "password",
            '@',
            "openlist.example/d/movie.mkv");
        WithStrm(descriptor, path =>
            Assert.ThrowsExactly<ArgumentException>(() =>
                RemoteMediaSourcePolicy.RequireDirectStrmSource(
                    path,
                    CreateSource(descriptor))));
    }

    [TestMethod]
    public void TryCanonicalizeHttpUrl_BoundsDescriptorWithoutReadingMedia()
    {
        var accepted = "https://media.example/" + new string('a', 16000);
        var rejected = "https://media.example/" + new string('a', 16384);

        Assert.IsTrue(RemoteMediaSourcePolicy.TryCanonicalizeHttpUrl(accepted, out _));
        Assert.IsFalse(RemoteMediaSourcePolicy.TryCanonicalizeHttpUrl(rejected, out _));
    }

    private static MediaSourceInfo CreateSource(string path) => new()
    {
        Protocol = MediaProtocol.Http,
        Path = path,
    };

    private static void WithStrm(string contents, Action<string> action) =>
        WithFile("movie.strm", contents, action);

    private static void WithFile(string name, string contents, Action<string> action)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "emby-external-player-strm-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, name);
            File.WriteAllText(path, contents, new UTF8Encoding(false));
            action(path);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
