using System.Net;
using Emby.ExternalPlayer.Services;

namespace Emby.ExternalPlayer.Tests;

[TestClass]
public sealed class RemoteRedirectResolverTests
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

    [TestMethod]
    public async Task ResolveAsync_ReturnsTemporaryCdnRedirectWithoutInterpretingItsQuery()
    {
        var handler = new RecordingHandler(_ => Redirect(
            HttpStatusCode.Redirect,
            "https://cdn.example/movie.mkv?t=1700003600&u=123&s=26214400"));
        var resolver = new RemoteRedirectResolver(handler);

        var result = await resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "IINA/1.4.0",
            Now,
            CancellationToken.None);

        Assert.AreEqual(
            "https://cdn.example/movie.mkv?t=1700003600&u=123&s=26214400",
            result.Url);
        Assert.AreEqual(1, handler.RequestCount);
        Assert.AreEqual("bytes=0-0", handler.Range);
        Assert.AreEqual("IINA/1.4.0", handler.UserAgent);
        Assert.IsFalse(handler.SentAuthorization);
        Assert.IsFalse(handler.SentPluginTicketHeader);
    }

    [TestMethod]
    public async Task ResolveAsync_AcceptsRelative307Redirect()
    {
        var handler = new RecordingHandler(_ => Redirect(
            HttpStatusCode.TemporaryRedirect,
            "/temporary/movie.mkv?t=1700003600&u=123&s=26214400"));
        var resolver = new RemoteRedirectResolver(handler);

        var result = await resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            null,
            Now,
            CancellationToken.None);

        Assert.AreEqual(
            "https://origin.example/temporary/movie.mkv?t=1700003600&u=123&s=26214400",
            result.Url);
    }

    [TestMethod]
    public async Task ResolveAsync_ReusesARecentRedirectWithoutAnotherSourceRequest()
    {
        var handler = new RecordingHandler(_ => Redirect(
            HttpStatusCode.Redirect,
            "https://cdn.example/movie.mkv?t=1700003600&u=123&s=26214400"));
        var resolver = new RemoteRedirectResolver(handler);

        var first = await resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now,
            CancellationToken.None);
        var second = await resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now.AddSeconds(20),
            CancellationToken.None);

        Assert.AreSame(first, second);
        Assert.AreEqual(1, handler.RequestCount);
    }

    [TestMethod]
    public async Task ResolveAsync_DoesNotShareAUserAgentSpecificRedirect()
    {
        var handler = new RecordingHandler(_ => Redirect(
            HttpStatusCode.Redirect,
            "https://cdn.example/movie.mkv?t=1700003600&u=123&s=26214400"));
        var resolver = new RemoteRedirectResolver(handler);

        await resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now,
            CancellationToken.None);
        await resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "Safari",
            Now.AddSeconds(1),
            CancellationToken.None);

        Assert.AreEqual(2, handler.RequestCount);
    }

    [TestMethod]
    public async Task ResolveAsync_RefreshesTheSourceAfterItsLeaseWithoutContactingTheCdn()
    {
        var sourceAttempt = 0;
        var handler = new RecordingHandler(request =>
        {
            Assert.AreEqual("origin.example", request.RequestUri!.Host);
            var attempt = Interlocked.Increment(ref sourceAttempt);
            return Redirect(
                HttpStatusCode.Redirect,
                "https://cdn.example/movie-" + attempt + ".mkv?temporary=opaque");
        });
        var resolver = new RemoteRedirectResolver(handler);

        var first = await resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now,
            CancellationToken.None);
        var fresh = await resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now.AddSeconds(20),
            CancellationToken.None);
        var refreshed = await resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now.AddSeconds(31),
            CancellationToken.None);

        Assert.AreSame(first, fresh);
        Assert.AreNotSame(first, refreshed);
        Assert.AreEqual("https://cdn.example/movie-2.mkv?temporary=opaque", refreshed.Url);
        Assert.AreEqual(2, sourceAttempt);
        Assert.AreEqual(2, handler.RequestCount);
    }

    [TestMethod]
    public async Task ResolveAsync_DoesNotRetainARedirectBeyondTheSingleLease()
    {
        var sourceAttempt = 0;
        var handler = new RecordingHandler(_ => Redirect(
            HttpStatusCode.Redirect,
            "https://cdn.example/movie-" + Interlocked.Increment(ref sourceAttempt) + ".mkv"));
        var resolver = new RemoteRedirectResolver(handler);

        var first = await resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now,
            CancellationToken.None);
        var second = await resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now.AddSeconds(31),
            CancellationToken.None);

        Assert.AreEqual(2, handler.RequestCount);
        Assert.AreNotEqual(first.Url, second.Url);
    }

    [TestMethod]
    public async Task ResolveAsync_DoesNotRejectOrExpireAUrlBasedOnOpaqueQueryNames()
    {
        const string destination =
            "https://cdn.example/movie.mkv?t=1&u=unknown&s=changed-format";
        var handler = new RecordingHandler(_ => Redirect(HttpStatusCode.Redirect, destination));
        var resolver = new RemoteRedirectResolver(handler);

        var first = await resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now,
            CancellationToken.None);
        var second = await resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now.AddSeconds(20),
            CancellationToken.None);

        Assert.AreEqual(destination, first.Url);
        Assert.AreSame(first, second);
        Assert.AreEqual(1, handler.RequestCount);
    }

    [TestMethod]
    public async Task ResolveAsync_UsesFailureCooldownWithoutReturningTheExpiredUrl()
    {
        var sourceAttempt = 0;
        var handler = new RecordingHandler(request =>
        {
            Assert.AreEqual("origin.example", request.RequestUri!.Host);
            var attempt = Interlocked.Increment(ref sourceAttempt);
            if (attempt == 2)
            {
                return new HttpResponseMessage(HttpStatusCode.BadGateway);
            }
            return Redirect(
                HttpStatusCode.Redirect,
                "https://cdn.example/movie-" + attempt + ".mkv?opaque=1");
        });
        var resolver = new RemoteRedirectResolver(handler);

        var first = await resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now,
            CancellationToken.None);
        var failure = await Assert.ThrowsExactlyAsync<RemoteSourceUnavailableException>(() =>
            resolver.ResolveAsync(
                "https://origin.example/d/movie.mkv?sign=long-lived",
                "libmpv",
                Now.AddSeconds(31),
                CancellationToken.None));
        var cooldown = await Assert.ThrowsExactlyAsync<RemoteSourceUnavailableException>(() =>
            resolver.ResolveAsync(
                "https://origin.example/d/movie.mkv?sign=long-lived",
                "libmpv",
                Now.AddSeconds(34),
                CancellationToken.None));
        var recovered = await resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now.AddSeconds(37),
            CancellationToken.None);

        Assert.AreEqual("https://cdn.example/movie-1.mkv?opaque=1", first.Url);
        Assert.AreEqual(5, failure.RetryAfterSeconds);
        Assert.IsTrue(cooldown.RetryAfterSeconds is >= 2 and <= 3);
        Assert.AreEqual("https://cdn.example/movie-3.mkv?opaque=1", recovered.Url);
        Assert.AreEqual(3, sourceAttempt);
        Assert.AreEqual(3, handler.RequestCount);
    }

    [TestMethod]
    public async Task ResolveAsync_CollapsesConcurrentSourceRefreshForAnExpiredLease()
    {
        var sourceAttempt = 0;
        var refreshResponse = new TaskCompletionSource<HttpResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RecordingHandler((request, _) =>
        {
            Assert.AreEqual("origin.example", request.RequestUri!.Host);
            if (Interlocked.Increment(ref sourceAttempt) == 1)
            {
                return Task.FromResult(Redirect(
                    HttpStatusCode.Redirect,
                    "https://cdn.example/movie-1.mkv?opaque=1"));
            }
            return refreshResponse.Task;
        });
        var resolver = new RemoteRedirectResolver(handler);

        await resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now,
            CancellationToken.None);
        var first = resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now.AddSeconds(31),
            CancellationToken.None);
        var second = resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now.AddSeconds(31),
            CancellationToken.None);

        Assert.AreEqual(2, handler.RequestCount);
        refreshResponse.SetResult(Redirect(
            HttpStatusCode.Redirect,
            "https://cdn.example/movie-2.mkv?opaque=1"));
        var results = await Task.WhenAll(first, second);

        Assert.AreSame(results[0], results[1]);
        Assert.AreEqual("https://cdn.example/movie-2.mkv?opaque=1", results[0].Url);
        Assert.AreEqual(2, sourceAttempt);
        Assert.AreEqual(2, handler.RequestCount);
    }

    [TestMethod]
    public async Task ResolveAsync_SharesBoundedSourceRetryAfterAcrossUserAgents()
    {
        var sourceAttempt = 0;
        var handler = new RecordingHandler(_ =>
        {
            var attempt = Interlocked.Increment(ref sourceAttempt);
            if (attempt == 1)
            {
                var response = new HttpResponseMessage((HttpStatusCode)429);
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
                    TimeSpan.FromSeconds(90));
                return response;
            }
            return Redirect(
                HttpStatusCode.Redirect,
                "https://cdn.example/movie-" + attempt + ".mkv?opaque=1");
        });
        var resolver = new RemoteRedirectResolver(handler);

        var first = await Assert.ThrowsExactlyAsync<RemoteSourceUnavailableException>(() =>
            resolver.ResolveAsync(
                "https://origin.example/d/movie.mkv?sign=long-lived",
                "client-a",
                Now,
                CancellationToken.None));
        var cooldown = await Assert.ThrowsExactlyAsync<RemoteSourceUnavailableException>(() =>
            resolver.ResolveAsync(
                "https://origin.example/d/movie.mkv?sign=long-lived",
                "client-b",
                Now.AddSeconds(45),
                CancellationToken.None));
        var recovered = await resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "client-c",
            Now.AddSeconds(61),
            CancellationToken.None);

        Assert.AreEqual(60, first.RetryAfterSeconds);
        Assert.IsTrue(cooldown.RetryAfterSeconds is >= 15 and <= 16);
        Assert.AreEqual("https://cdn.example/movie-2.mkv?opaque=1", recovered.Url);
        Assert.AreEqual(2, sourceAttempt);
        Assert.AreEqual(2, handler.RequestCount);
    }

    [TestMethod]
    public async Task ResolveAsync_ConcurrentSuccessDoesNotClearSourceRetryAfter()
    {
        var limitedResponse = new TaskCompletionSource<HttpResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var successfulResponse = new TaskCompletionSource<HttpResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RecordingHandler((request, _) =>
            request.Headers.UserAgent.ToString() == "client-a"
                ? limitedResponse.Task
                : successfulResponse.Task);
        var resolver = new RemoteRedirectResolver(handler);
        const string source = "https://origin.example/d/movie.mkv?sign=long-lived";

        var limited = resolver.ResolveAsync(source, "client-a", Now, CancellationToken.None);
        var successful = resolver.ResolveAsync(source, "client-b", Now, CancellationToken.None);
        var limitedMessage = new HttpResponseMessage((HttpStatusCode)429);
        limitedMessage.Headers.RetryAfter =
            new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(60));
        limitedResponse.SetResult(limitedMessage);
        await Assert.ThrowsExactlyAsync<RemoteSourceUnavailableException>(() => limited);
        successfulResponse.SetResult(Redirect(
            HttpStatusCode.Redirect,
            "https://cdn.example/movie.mkv?temporary=1"));
        await successful;

        var cooldown = await Assert.ThrowsExactlyAsync<RemoteSourceUnavailableException>(() =>
            resolver.ResolveAsync(source, "client-c", Now.AddSeconds(1), CancellationToken.None));

        Assert.IsTrue(cooldown.RetryAfterSeconds is >= 59 and <= 60);
        Assert.AreEqual(2, handler.RequestCount);
    }

    [TestMethod]
    public async Task ResolveAsync_ConcurrentShortFailureDoesNotShortenSourceRetryAfter()
    {
        var longRetryResponse = new TaskCompletionSource<HttpResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var shortRetryResponse = new TaskCompletionSource<HttpResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RecordingHandler((request, _) =>
            request.Headers.UserAgent.ToString() == "client-a"
                ? longRetryResponse.Task
                : shortRetryResponse.Task);
        var resolver = new RemoteRedirectResolver(handler);
        const string source = "https://origin.example/d/movie.mkv?sign=long-lived";

        var longRetry = resolver.ResolveAsync(source, "client-a", Now, CancellationToken.None);
        var shortRetry = resolver.ResolveAsync(source, "client-b", Now, CancellationToken.None);
        var limitedMessage = new HttpResponseMessage((HttpStatusCode)429);
        limitedMessage.Headers.RetryAfter =
            new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(60));
        longRetryResponse.SetResult(limitedMessage);
        await Assert.ThrowsExactlyAsync<RemoteSourceUnavailableException>(() => longRetry);
        shortRetryResponse.SetResult(new HttpResponseMessage(HttpStatusCode.BadGateway));
        await Assert.ThrowsExactlyAsync<RemoteSourceUnavailableException>(() => shortRetry);

        var cooldown = await Assert.ThrowsExactlyAsync<RemoteSourceUnavailableException>(() =>
            resolver.ResolveAsync(source, "client-c", Now.AddSeconds(6), CancellationToken.None));

        Assert.IsTrue(cooldown.RetryAfterSeconds is >= 54 and <= 55);
        Assert.AreEqual(2, handler.RequestCount);
    }

    [TestMethod]
    public async Task ResolveAsync_DoesNotCacheANonTransientSourceFailure()
    {
        var sourceAttempt = 0;
        var handler = new RecordingHandler(_ =>
        {
            var attempt = Interlocked.Increment(ref sourceAttempt);
            if (attempt == 2)
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden);
            }
            return Redirect(
                HttpStatusCode.Redirect,
                "https://cdn.example/movie-" + attempt + ".mkv?opaque=1");
        });
        var resolver = new RemoteRedirectResolver(handler);

        await resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now,
            CancellationToken.None);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now.AddSeconds(31),
            CancellationToken.None));
        var recovered = await resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now.AddSeconds(32),
            CancellationToken.None);

        Assert.AreEqual("https://cdn.example/movie-3.mkv?opaque=1", recovered.Url);
        Assert.AreEqual(3, sourceAttempt);
        Assert.AreEqual(3, handler.RequestCount);
    }

    [TestMethod]
    public async Task ResolveAsync_RejectsCredentialCopyDuringSourceRefresh()
    {
        var sourceAttempt = 0;
        var handler = new RecordingHandler(_ =>
        {
            var attempt = Interlocked.Increment(ref sourceAttempt);
            if (attempt == 2)
            {
                return Redirect(
                    HttpStatusCode.Redirect,
                    "https://cdn.example/movie.mkv?auth_key=long-lived");
            }
            return Redirect(
                HttpStatusCode.Redirect,
                "https://cdn.example/movie-" + attempt + ".mkv?opaque=1");
        });
        var resolver = new RemoteRedirectResolver(handler);

        await resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now,
            CancellationToken.None);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now.AddSeconds(31),
            CancellationToken.None));
        var recovered = await resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now.AddSeconds(32),
            CancellationToken.None);

        Assert.AreEqual("https://cdn.example/movie-3.mkv?opaque=1", recovered.Url);
        Assert.AreEqual(3, sourceAttempt);
        Assert.AreEqual(3, handler.RequestCount);
    }

    [TestMethod]
    public async Task ResolveAsync_CollapsesConcurrentRequestsForTheSameSource()
    {
        var response = new TaskCompletionSource<HttpResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RecordingHandler((_, _) => response.Task);
        var resolver = new RemoteRedirectResolver(handler);

        var first = resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now,
            CancellationToken.None);
        var second = resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now,
            CancellationToken.None);

        Assert.AreEqual(1, handler.RequestCount);
        response.SetResult(Redirect(
            HttpStatusCode.Redirect,
            "https://cdn.example/movie.mkv?t=1700003600&u=123&s=26214400"));
        var results = await Task.WhenAll(first, second);

        Assert.AreSame(results[0], results[1]);
        Assert.AreEqual(1, handler.RequestCount);
    }

    [TestMethod]
    public async Task ResolveAsync_BoundsWaitersForOneSharedResolution()
    {
        var response = new TaskCompletionSource<HttpResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RecordingHandler((_, _) => response.Task);
        var resolver = new RemoteRedirectResolver(handler);
        var waiters = new List<Task<ResolvedRemoteStream>>();

        for (var index = 0; index < 64; index++)
        {
            waiters.Add(resolver.ResolveAsync(
                "https://origin.example/d/movie.mkv?sign=long-lived",
                "libmpv",
                Now,
                CancellationToken.None));
        }
        var throttled = await Assert.ThrowsExactlyAsync<RemoteResolutionThrottledException>(() =>
            resolver.ResolveAsync(
                "https://origin.example/d/movie.mkv?sign=long-lived",
                "libmpv",
                Now,
                CancellationToken.None));

        Assert.AreEqual(1, throttled.RetryAfterSeconds);
        Assert.AreEqual(1, handler.RequestCount);
        response.SetResult(Redirect(
            HttpStatusCode.Redirect,
            "https://cdn.example/movie.mkv?t=1700003600&u=123&s=26214400"));
        await Task.WhenAll(waiters);
    }

    [TestMethod]
    public async Task ResolveAsync_FirstCallerCancellationDoesNotCancelSharedResolution()
    {
        var response = new TaskCompletionSource<HttpResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RecordingHandler((_, cancellationToken) =>
        {
            cancellationToken.Register(() => response.TrySetCanceled(cancellationToken));
            return response.Task;
        });
        var resolver = new RemoteRedirectResolver(handler);
        using var firstCancellation = new CancellationTokenSource();

        var first = resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now,
            firstCancellation.Token);
        var second = resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now,
            CancellationToken.None);
        firstCancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => first);
        response.TrySetResult(Redirect(
            HttpStatusCode.Redirect,
            "https://cdn.example/movie.mkv?t=1700003600&u=123&s=26214400"));
        var result = await second;

        Assert.AreEqual(
            "https://cdn.example/movie.mkv?t=1700003600&u=123&s=26214400",
            result.Url);
        Assert.AreEqual(1, handler.RequestCount);
    }

    [TestMethod]
    public async Task ResolveAsync_LastCallerCancellationStopsTheSourceRequest()
    {
        var sourceCanceled = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<HttpResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var sourceAttempt = 0;
        var handler = new RecordingHandler((_, cancellationToken) =>
        {
            if (Interlocked.Increment(ref sourceAttempt) > 1)
            {
                return Task.FromResult(Redirect(
                    HttpStatusCode.Redirect,
                    "https://cdn.example/movie.mkv?fresh=1"));
            }
            cancellationToken.Register(() =>
            {
                sourceCanceled.TrySetResult(true);
                response.TrySetCanceled(cancellationToken);
            });
            return response.Task;
        });
        var resolver = new RemoteRedirectResolver(handler);
        using var callerCancellation = new CancellationTokenSource();
        var resolution = resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now,
            callerCancellation.Token);

        callerCancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => resolution);
        Assert.IsTrue(await sourceCanceled.Task.WaitAsync(TimeSpan.FromSeconds(1)));
        var recovered = await resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now.AddSeconds(1),
            CancellationToken.None);

        Assert.AreEqual("https://cdn.example/movie.mkv?fresh=1", recovered.Url);
        Assert.AreEqual(2, handler.RequestCount);
    }

    [TestMethod]
    public async Task ResolveAsync_RetiredOwnerCannotOverwriteANewerResolution()
    {
        var sourceAttempt = 0;
        var retiredResponse = new TaskCompletionSource<HttpResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RecordingHandler((_, _) =>
        {
            var attempt = Interlocked.Increment(ref sourceAttempt);
            return attempt switch
            {
                1 => Task.FromResult(Redirect(
                    HttpStatusCode.Redirect,
                    "https://cdn.example/movie-initial.mkv")),
                2 => retiredResponse.Task,
                _ => Task.FromResult(Redirect(
                    HttpStatusCode.Redirect,
                    "https://cdn.example/movie-new.mkv")),
            };
        });
        var resolver = new RemoteRedirectResolver(handler);
        await resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now,
            CancellationToken.None);

        using var retiredCancellation = new CancellationTokenSource();
        var retired = resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now.AddSeconds(31),
            retiredCancellation.Token);
        retiredCancellation.Cancel();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => retired);

        var newer = await resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now.AddSeconds(32),
            CancellationToken.None);
        retiredResponse.SetResult(Redirect(
            HttpStatusCode.Redirect,
            "https://cdn.example/movie-retired.mkv"));
        Assert.IsTrue(SpinWait.SpinUntil(
            () => AvailableResolutionSlots(resolver) == 16,
            TimeSpan.FromSeconds(1)));
        var cached = await resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now.AddSeconds(33),
            CancellationToken.None);

        Assert.AreEqual("https://cdn.example/movie-new.mkv", newer.Url);
        Assert.AreSame(newer, cached);
        Assert.AreEqual(3, handler.RequestCount);
    }

    [TestMethod]
    public async Task RemoveExpired_RemovesRedirectAndFailureEntries()
    {
        var redirectAttempt = 0;
        var redirectHandler = new RecordingHandler(_ => Redirect(
            HttpStatusCode.Redirect,
            "https://cdn.example/movie-" +
            Interlocked.Increment(ref redirectAttempt) +
            ".mkv"));
        var redirectResolver = new RemoteRedirectResolver(redirectHandler);
        await redirectResolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now,
            CancellationToken.None);

        Assert.AreEqual(0, redirectResolver.RemoveExpired(Now.AddSeconds(29)));
        Assert.AreEqual(1, redirectResolver.RemoveExpired(Now.AddSeconds(31)));
        var refreshed = await redirectResolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now.AddSeconds(31),
            CancellationToken.None);

        Assert.AreEqual("https://cdn.example/movie-2.mkv", refreshed.Url);

        var failureAttempt = 0;
        var failureHandler = new RecordingHandler(_ =>
        {
            if (Interlocked.Increment(ref failureAttempt) == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }
            return Redirect(HttpStatusCode.Redirect, "https://cdn.example/recovered.mkv");
        });
        var failureResolver = new RemoteRedirectResolver(failureHandler);
        await Assert.ThrowsExactlyAsync<RemoteSourceUnavailableException>(() =>
            failureResolver.ResolveAsync(
                "https://origin.example/d/movie.mkv?sign=long-lived",
                "libmpv",
                Now,
                CancellationToken.None));

        Assert.AreEqual(0, failureResolver.RemoveExpired(Now.AddSeconds(4)));
        Assert.AreEqual(1, failureResolver.RemoveExpired(Now.AddSeconds(6)));
        var recovered = await failureResolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now.AddSeconds(6),
            CancellationToken.None);

        Assert.AreEqual("https://cdn.example/recovered.mkv", recovered.Url);
    }

    [TestMethod]
    public async Task RemoveExpired_RemovesIdleSourceBudgets()
    {
        var handler = new RecordingHandler(_ => Redirect(
            HttpStatusCode.Redirect,
            "https://cdn.example/movie.mkv"));
        var resolver = new RemoteRedirectResolver(handler);
        await resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "client-a",
            Now,
            CancellationToken.None);

        Assert.AreEqual(1, resolver.RemoveExpired(Now.AddSeconds(31)));
        Assert.AreEqual(0, resolver.RemoveExpired(Now.AddMinutes(4)));
        Assert.AreEqual(1, resolver.RemoveExpired(Now.AddMinutes(5).AddSeconds(1)));
    }

    [TestMethod]
    public async Task ResolveAsync_BoundsConcurrentUserAgentVariantsForOneSource()
    {
        var response = new TaskCompletionSource<HttpResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RecordingHandler((_, _) => response.Task);
        var resolver = new RemoteRedirectResolver(handler);

        var first = resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "first",
            Now,
            CancellationToken.None);
        var second = resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "second",
            Now,
            CancellationToken.None);
        await Assert.ThrowsExactlyAsync<RemoteResolutionThrottledException>(() =>
            resolver.ResolveAsync(
                "https://origin.example/d/movie.mkv?sign=long-lived",
                "third",
                Now,
                CancellationToken.None));

        response.SetResult(Redirect(
            HttpStatusCode.Redirect,
            "https://cdn.example/movie.mkv?t=1700003600&u=123&s=26214400"));
        await Task.WhenAll(first, second);
        Assert.AreEqual(2, handler.RequestCount);
    }

    [TestMethod]
    public async Task ResolveAsync_RateLimitsRepeatedUserAgentCacheMissesAndClearResetsTheBudget()
    {
        var handler = new RecordingHandler(_ => Redirect(
            HttpStatusCode.Redirect,
            "https://cdn.example/movie.mkv?t=1700003600&u=123&s=26214400"));
        var resolver = new RemoteRedirectResolver(handler);
        const string source = "https://origin.example/d/movie.mkv?sign=long-lived";

        for (var index = 0; index < 12; index++)
        {
            await resolver.ResolveAsync(
                source,
                "agent-" + index,
                Now,
                CancellationToken.None);
        }
        await Assert.ThrowsExactlyAsync<RemoteResolutionThrottledException>(() =>
            resolver.ResolveAsync(source, "agent-12", Now, CancellationToken.None));
        resolver.Clear();
        await resolver.ResolveAsync(
            source,
            "agent-13",
            Now,
            CancellationToken.None);

        Assert.AreEqual(13, handler.RequestCount);
    }

    [TestMethod]
    public async Task Clear_CancelsResolverOwnedSourceRequests()
    {
        var response = new TaskCompletionSource<HttpResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RecordingHandler((_, cancellationToken) =>
        {
            cancellationToken.Register(() => response.TrySetCanceled(cancellationToken));
            return response.Task;
        });
        var resolver = new RemoteRedirectResolver(handler);
        var resolution = resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now,
            CancellationToken.None);

        resolver.Clear();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => resolution);
    }

    [TestMethod]
    public async Task ResolveAsync_BoundsGlobalConcurrentSourceRequests()
    {
        var handler = new RecordingHandler((_, cancellationToken) =>
        {
            var response = new TaskCompletionSource<HttpResponseMessage>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() => response.TrySetCanceled(cancellationToken));
            return response.Task;
        });
        var resolver = new RemoteRedirectResolver(handler);
        var resolutions = new List<Task<ResolvedRemoteStream>>();
        for (var index = 0; index < 16; index++)
        {
            resolutions.Add(resolver.ResolveAsync(
                "https://origin.example/d/movie-" + index + ".mkv?sign=long-lived",
                "libmpv",
                Now,
                CancellationToken.None));
        }

        await Assert.ThrowsExactlyAsync<RemoteResolutionThrottledException>(() =>
            resolver.ResolveAsync(
                "https://origin.example/d/overflow.mkv?sign=long-lived",
                "libmpv",
                Now,
                CancellationToken.None));
        resolver.Clear();
        foreach (var resolution in resolutions)
        {
            await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => resolution);
        }

        Assert.AreEqual(16, handler.RequestCount);
    }

    [TestMethod]
    public async Task ResolveAsync_CachesOnlyTheFailureCooldown()
    {
        var attempt = 0;
        var handler = new RecordingHandler(_ => ++attempt == 1
            ? new HttpResponseMessage(HttpStatusCode.BadGateway)
            : Redirect(
                HttpStatusCode.Redirect,
                "https://cdn.example/movie.mkv?t=1700003600&u=123&s=26214400"));
        var resolver = new RemoteRedirectResolver(handler);

        await Assert.ThrowsExactlyAsync<RemoteSourceUnavailableException>(() => resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now,
            CancellationToken.None));
        await Assert.ThrowsExactlyAsync<RemoteSourceUnavailableException>(() => resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now.AddSeconds(1),
            CancellationToken.None));
        var result = await resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now.AddSeconds(6),
            CancellationToken.None);

        Assert.AreEqual("https://cdn.example/movie.mkv?t=1700003600&u=123&s=26214400", result.Url);
        Assert.AreEqual(2, handler.RequestCount);
    }

    [TestMethod]
    public async Task Clear_RemovesCachedRedirects()
    {
        var handler = new RecordingHandler(_ => Redirect(
            HttpStatusCode.Redirect,
            "https://cdn.example/movie.mkv?t=1700003600&u=123&s=26214400"));
        var resolver = new RemoteRedirectResolver(handler);

        await resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now,
            CancellationToken.None);
        resolver.Clear();
        await resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            "libmpv",
            Now.AddSeconds(1),
            CancellationToken.None);

        Assert.AreEqual(2, handler.RequestCount);
    }

    [TestMethod]
    public async Task ResolveAsync_RejectsNonRedirectWithoutReadingBody()
    {
        var content = new ThrowingContent();
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content,
        });
        var resolver = new RemoteRedirectResolver(handler);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            null,
            Now,
            CancellationToken.None));

        Assert.IsFalse(content.WasRead);
        Assert.AreEqual(1, handler.RequestCount);
    }

    [TestMethod]
    public async Task ResolveAsync_RejectsOriginalLongLivedSignatureInDestination()
    {
        var handler = new RecordingHandler(_ => Redirect(
            HttpStatusCode.Redirect,
            "https://cdn.example/movie.mkv?sign=long-lived&t=1700003600"));
        var resolver = new RemoteRedirectResolver(handler);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            null,
            Now,
            CancellationToken.None));
    }

    [TestMethod]
    public async Task ResolveAsync_RejectsOriginalCredentialInAnyDuplicateParameter()
    {
        var handler = new RecordingHandler(_ => Redirect(
            HttpStatusCode.Redirect,
            "https://cdn.example/movie.mkv?token=temporary&token=long-lived"));
        var resolver = new RemoteRedirectResolver(handler);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?token=long-lived",
            null,
            Now,
            CancellationToken.None));
    }

    [TestMethod]
    public async Task ResolveAsync_RejectsOriginalCredentialRenamedInDestination()
    {
        var handler = new RecordingHandler(_ => Redirect(
            HttpStatusCode.Redirect,
            "https://cdn.example/movie.mkv?auth_key=long-lived"));
        var resolver = new RemoteRedirectResolver(handler);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            null,
            Now,
            CancellationToken.None));
    }

    [TestMethod]
    public async Task ResolveAsync_RejectsOriginalCredentialCopiedIntoDestinationPath()
    {
        var handler = new RecordingHandler(_ => Redirect(
            HttpStatusCode.Redirect,
            "https://cdn.example/download/long-lived/movie.mkv"));
        var resolver = new RemoteRedirectResolver(handler);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            null,
            Now,
                CancellationToken.None));
    }

    [TestMethod]
    public async Task ResolveAsync_RejectsShortCredentialCopiedAsAPathSegment()
    {
        var handler = new RecordingHandler(_ => Redirect(
            HttpStatusCode.Redirect,
            "https://cdn.example/download/1234567/movie.mkv"));
        var resolver = new RemoteRedirectResolver(handler);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?api_key=1234567",
            null,
            Now,
            CancellationToken.None));
    }

    [TestMethod]
    public async Task ResolveAsync_RejectsCredentialCopiedIntoDestinationHost()
    {
        var handler = new RecordingHandler(_ => Redirect(
            HttpStatusCode.Redirect,
            "https://abcdef123456.cdn.example/movie.mkv"));
        var resolver = new RemoteRedirectResolver(handler);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=abcdef123456",
            null,
            Now,
            CancellationToken.None));
    }

    [TestMethod]
    public async Task ResolveAsync_RejectsUnsafeDestination()
    {
        var handler = new RecordingHandler(_ => Redirect(
            HttpStatusCode.Redirect,
            "file:///etc/passwd"));
        var resolver = new RemoteRedirectResolver(handler);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => resolver.ResolveAsync(
            "https://origin.example/d/movie.mkv?sign=long-lived",
            null,
            Now,
            CancellationToken.None));
    }

    private static HttpResponseMessage Redirect(HttpStatusCode statusCode, string location)
    {
        var response = new HttpResponseMessage(statusCode);
        response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
        return response;
    }

    private static int AvailableResolutionSlots(RemoteRedirectResolver resolver)
    {
        var field = typeof(RemoteRedirectResolver).GetField(
            "resolutionSlots",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        var slots = (SemaphoreSlim?)field?.GetValue(resolver);
        return slots?.CurrentCount ?? -1;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<
            HttpRequestMessage,
            CancellationToken,
            Task<HttpResponseMessage>> responseFactory;

        private int requestCount;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
            : this((request, _) => Task.FromResult(responseFactory(request)))
        {
        }

        public RecordingHandler(Func<
            HttpRequestMessage,
            CancellationToken,
            Task<HttpResponseMessage>> responseFactory)
        {
            this.responseFactory = responseFactory;
        }

        public int RequestCount => Volatile.Read(ref requestCount);

        public string Range { get; private set; } = string.Empty;

        public string UserAgent { get; private set; } = string.Empty;

        public bool SentAuthorization { get; private set; }

        public bool SentPluginTicketHeader { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref requestCount);
            Range = request.Headers.Range?.ToString() ?? string.Empty;
            UserAgent = string.Join(" ", request.Headers.UserAgent.Select(value => value.ToString()));
            SentAuthorization = request.Headers.Authorization is not null;
            SentPluginTicketHeader = request.Headers.Any(header =>
                header.Key.StartsWith("X-Emby-", StringComparison.OrdinalIgnoreCase));
            return responseFactory(request, cancellationToken);
        }
    }

    private sealed class ThrowingContent : HttpContent
    {
        public bool WasRead { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            WasRead = true;
            throw new InvalidOperationException("The media body must not be read.");
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 10L * 1024 * 1024 * 1024;
            return true;
        }
    }
}
