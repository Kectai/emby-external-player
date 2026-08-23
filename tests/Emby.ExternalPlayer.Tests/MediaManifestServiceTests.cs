using System.Reflection;
using Emby.ExternalPlayer.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Users;

namespace Emby.ExternalPlayer.Tests;

[TestClass]
public sealed class MediaManifestServiceTests
{
    [TestMethod]
    public void TryGetContext_ReturnsFalseForANonVideoDetailItemWithoutLoadingMediaSources()
    {
        var series = new Series();
        var mediaSourceCalls = 0;
        var library = CreateProxy<ILibraryManager>((method, _) => method.Name switch
        {
            nameof(ILibraryManager.GetInternalId) => 42L,
            nameof(ILibraryManager.GetItemById) => series,
            _ => DefaultValue(method.ReturnType),
        });
        var mediaSources = CreateProxy<IMediaSourceManager>((method, _) =>
        {
            mediaSourceCalls++;
            return DefaultValue(method.ReturnType);
        });
        var userData = CreateProxy<IUserDataManager>((method, _) => DefaultValue(method.ReturnType));
        var service = new MediaManifestService(library, mediaSources, userData);

        var found = service.TryGetContext("series-item", new User(), out var context);

        Assert.IsFalse(found);
        Assert.IsNull(context);
        Assert.AreEqual(0, mediaSourceCalls);
    }

    [TestMethod]
    public void TryGetContext_ReturnsFalseForAMalformedItemIdWithoutLoadingTheItem()
    {
        var itemCalls = 0;
        var library = CreateProxy<ILibraryManager>((method, _) => method.Name switch
        {
            nameof(ILibraryManager.GetInternalId) => throw new FormatException("invalid item id"),
            nameof(ILibraryManager.GetItemById) => CountCall(ref itemCalls, method.ReturnType),
            _ => DefaultValue(method.ReturnType),
        });
        var mediaSources = CreateProxy<IMediaSourceManager>((method, _) => DefaultValue(method.ReturnType));
        var userData = CreateProxy<IUserDataManager>((method, _) => DefaultValue(method.ReturnType));
        var service = new MediaManifestService(library, mediaSources, userData);

        var found = service.TryGetContext("invalid-item", CreatePlayableUser(), out var context);

        Assert.IsFalse(found);
        Assert.IsNull(context);
        Assert.AreEqual(0, itemCalls);
    }

    [TestMethod]
    public void TryGetContext_DoesNotHideMediaSourceFailuresForAPlayableVideo()
    {
        var movie = new VisibleMovie();
        var library = CreateProxy<ILibraryManager>((method, _) => method.Name switch
        {
            nameof(ILibraryManager.GetInternalId) => 42L,
            nameof(ILibraryManager.GetItemById) => movie,
            _ => DefaultValue(method.ReturnType),
        });
        var mediaSources = CreateProxy<IMediaSourceManager>((method, _) =>
            method.Name == nameof(IMediaSourceManager.GetStaticMediaSources)
                ? throw new FormatException("invalid media source")
                : DefaultValue(method.ReturnType));
        var userData = CreateProxy<IUserDataManager>((method, _) => DefaultValue(method.ReturnType));
        var service = new MediaManifestService(library, mediaSources, userData);

        Assert.ThrowsExactly<FormatException>(() =>
            service.TryGetContext("video-item", CreatePlayableUser(), out _));
    }

    [TestMethod]
    public void TryGetContext_ReturnsAContextForAPlayableVideo()
    {
        var movie = new VisibleMovie();
        var sources = new List<MediaSourceInfo>();
        var library = CreateProxy<ILibraryManager>((method, _) => method.Name switch
        {
            nameof(ILibraryManager.GetInternalId) => 42L,
            nameof(ILibraryManager.GetItemById) => movie,
            _ => DefaultValue(method.ReturnType),
        });
        var mediaSources = CreateProxy<IMediaSourceManager>((method, _) =>
            method.Name == nameof(IMediaSourceManager.GetStaticMediaSources)
                ? sources
                : DefaultValue(method.ReturnType));
        var userData = CreateProxy<IUserDataManager>((method, _) => DefaultValue(method.ReturnType));
        var service = new MediaManifestService(library, mediaSources, userData);

        var found = service.TryGetContext("video-item", CreatePlayableUser(), out var context);

        Assert.IsTrue(found);
        Assert.AreSame(movie, context.Item);
        Assert.AreSame(sources, context.MediaSources);
    }

    [TestMethod]
    public void TryGetContext_ReturnsFalseForAUserWithoutPlaybackPermission()
    {
        var movie = new VisibleMovie();
        var mediaSourceCalls = 0;
        var library = CreateProxy<ILibraryManager>((method, _) => method.Name switch
        {
            nameof(ILibraryManager.GetInternalId) => 42L,
            nameof(ILibraryManager.GetItemById) => movie,
            _ => DefaultValue(method.ReturnType),
        });
        var mediaSources = CreateProxy<IMediaSourceManager>((method, _) =>
            CountCall(ref mediaSourceCalls, method.ReturnType));
        var userData = CreateProxy<IUserDataManager>((method, _) => DefaultValue(method.ReturnType));
        var service = new MediaManifestService(library, mediaSources, userData);
        var user = CreatePlayableUser();
        user.Policy.EnableMediaPlayback = false;

        var found = service.TryGetContext("video-item", user, out var context);

        Assert.IsFalse(found);
        Assert.IsNull(context);
        Assert.AreEqual(0, mediaSourceCalls);
    }

    private static User CreatePlayableUser() => new User
    {
        Policy = new UserPolicy
        {
            EnableAllFolders = true,
            EnableMediaPlayback = true,
        },
    };

    private static object? CountCall(ref int calls, Type returnType)
    {
        calls++;
        return DefaultValue(returnType);
    }

    private static T CreateProxy<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, ManifestDispatchProxy>();
        ((ManifestDispatchProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    private static object? DefaultValue(Type type) =>
        type == typeof(void) ? null : type.IsValueType ? Activator.CreateInstance(type) : null;

    private sealed class VisibleMovie : Movie
    {
        public override bool IsVisible(User user) => true;
    }

    private class ManifestDispatchProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?>? Handler { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod is null
                ? throw new InvalidOperationException("A proxied method is required.")
                : Handler?.Invoke(targetMethod, args) ?? DefaultValue(targetMethod.ReturnType);
    }
}
