using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Emby.ExternalPlayer.Services;

public sealed class ResolvedRemoteStream
{
    public ResolvedRemoteStream(string url)
    {
        Url = url;
    }

    public string Url { get; }
}

public sealed class RemoteResolutionThrottledException : Exception
{
    public RemoteResolutionThrottledException(int retryAfterSeconds)
        : base("Too many remote STRM sources are being resolved.")
    {
        RetryAfterSeconds = Math.Max(1, retryAfterSeconds);
    }

    public int RetryAfterSeconds { get; }
}

public sealed class RemoteSourceUnavailableException : Exception
{
    public RemoteSourceUnavailableException(int retryAfterSeconds, Exception? innerException = null)
        : base("The remote STRM source is temporarily unavailable.", innerException)
    {
        RetryAfterSeconds = Math.Max(1, retryAfterSeconds);
    }

    public int RetryAfterSeconds { get; }
}

public sealed class RemoteRedirectResolver
{
    private const int MaximumCacheEntries = 256;
    private const int MaximumBudgetEntries = 512;
    private const int MaximumConcurrentResolutions = 16;
    private const int MaximumConcurrentResolutionsPerSource = 2;
    private const int MaximumWaitersPerResolution = 64;
    private const double SourceRequestBurst = 12;
    private const double SourceRequestsPerMinute = 30;

    private static readonly TimeSpan SourceRequestTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CacheLeaseLifetime = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan TransientFailureRetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaximumSourceRetryDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan SourceBudgetIdleLifetime = TimeSpan.FromMinutes(5);

    private static readonly HttpClient SharedClient = CreateClient();

    private readonly HttpClient client;
    private readonly object cacheSync = new();
    private readonly Dictionary<string, RedirectCacheEntry> cache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SourceFailureEntry> failures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PendingResolution> inFlight =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, SourceResolutionBudget> sourceBudgets =
        new(StringComparer.Ordinal);
    private readonly SemaphoreSlim resolutionSlots = new(
        MaximumConcurrentResolutions,
        MaximumConcurrentResolutions);
    private long cacheGeneration;

    public RemoteRedirectResolver()
        : this(SharedClient)
    {
    }

    public RemoteRedirectResolver(HttpMessageHandler handler)
        : this(new HttpClient(handler ?? throw new ArgumentNullException(nameof(handler)), true)
        {
            Timeout = SourceRequestTimeout,
        })
    {
    }

    private RemoteRedirectResolver(HttpClient client)
    {
        this.client = client;
    }

    public async Task<ResolvedRemoteStream> ResolveAsync(
        string sourceUrl,
        string? userAgent,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!RemoteMediaSourcePolicy.TryCanonicalizeHttpUrl(sourceUrl, out var canonicalSource) ||
            !Uri.TryCreate(canonicalSource, UriKind.Absolute, out _))
        {
            throw new ArgumentException("The remote STRM URL is invalid.", nameof(sourceUrl));
        }

        var normalizedUserAgent = NormalizeUserAgent(userAgent);
        var cacheKey = CreateCacheKey(canonicalSource!, normalizedUserAgent);
        var sourceKey = CreateCacheKey(canonicalSource!, string.Empty);
        PendingResolution pending;
        var ownsResolution = false;
        SourceResolutionBudget? sourceBudget = null;
        long generation = 0;
        lock (cacheSync)
        {
            RemoveExpiredCacheEntries(now);
            if (cache.TryGetValue(cacheKey, out var cached))
            {
                cached.LastAccessUtc = now;
                return cached.Stream;
            }
            RemoveExpiredFailureEntries(now);
            if (failures.TryGetValue(sourceKey, out var failure))
            {
                failure.LastAccessUtc = now;
                throw new RemoteSourceUnavailableException(
                    RemainingRetrySeconds(failure.RetryAfterUtc, now));
            }
            if (inFlight.TryGetValue(cacheKey, out pending!) && !pending.AcceptingWaiters)
            {
                inFlight.Remove(cacheKey);
                pending = null!;
            }
            if (pending is null)
            {
                if (!resolutionSlots.Wait(0))
                {
                    throw new RemoteResolutionThrottledException(5);
                }
                try
                {
                    sourceBudget = GetSourceBudget(sourceKey, now);
                    if (sourceBudget.ActiveResolutions >= MaximumConcurrentResolutionsPerSource)
                    {
                        throw new RemoteResolutionThrottledException(5);
                    }
                    var retryAfterSeconds = sourceBudget.TryConsume(now);
                    if (retryAfterSeconds > 0)
                    {
                        throw new RemoteResolutionThrottledException(retryAfterSeconds);
                    }
                    sourceBudget.ActiveResolutions++;
                }
                catch
                {
                    resolutionSlots.Release();
                    throw;
                }
                pending = new PendingResolution(cacheKey);
                inFlight.Add(cacheKey, pending);
                generation = cacheGeneration;
                ownsResolution = true;
            }
            if (pending.WaiterCount >= MaximumWaitersPerResolution)
            {
                throw new RemoteResolutionThrottledException(1);
            }
            pending.WaiterCount++;
        }

        if (ownsResolution)
        {
            ObserveFault(pending.Completion.Task);
            _ = ResolveAndPublishAsync(
                cacheKey,
                sourceKey,
                canonicalSource!,
                normalizedUserAgent,
                now,
                generation,
                sourceBudget!,
                pending);
        }

        try
        {
            return await AwaitWithCancellationAsync(
                    pending.Completion.Task,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            ReleaseWaiter(pending);
        }
    }

    public void Clear()
    {
        var cancellations = new List<PendingResolution>();
        lock (cacheSync)
        {
            cacheGeneration++;
            cache.Clear();
            failures.Clear();
            sourceBudgets.Clear();
            foreach (var pending in inFlight.Values)
            {
                pending.AcceptingWaiters = false;
                if (!pending.CancelPending)
                {
                    pending.CancelPending = true;
                    cancellations.Add(pending);
                }
            }
            inFlight.Clear();
        }
        foreach (var pending in cancellations)
        {
            CompletePendingCancellation(pending);
        }
    }

    private async Task ResolveAndPublishAsync(
        string cacheKey,
        string sourceKey,
        string sourceUrl,
        string userAgent,
        DateTimeOffset now,
        long generation,
        SourceResolutionBudget sourceBudget,
        PendingResolution pending)
    {
        var elapsed = Stopwatch.StartNew();
        try
        {
            var result = await ResolveCoreAsync(
                    sourceUrl,
                    userAgent,
                    pending.Cancellation.Token)
                .ConfigureAwait(false);
            var completedAt = now.Add(elapsed.Elapsed);
            lock (cacheSync)
            {
                if (OwnsResolution(cacheKey, generation, pending))
                {
                    AddCacheEntry(cacheKey, result, completedAt);
                }
            }
            pending.Completion.TrySetResult(result);
        }
        catch (Exception exception)
        {
            var completedAt = now.Add(elapsed.Elapsed);
            var unavailable = CreateSourceUnavailableException(
                exception,
                pending.Cancellation.Token);
            if (unavailable is not null)
            {
                lock (cacheSync)
                {
                    if (OwnsResolution(cacheKey, generation, pending))
                    {
                        AddFailureEntry(
                            sourceKey,
                            completedAt,
                            unavailable.RetryAfterSeconds);
                    }
                }
                pending.Completion.TrySetException(unavailable);
            }
            else
            {
                pending.Completion.TrySetException(exception);
            }
        }
        finally
        {
            var dispose = false;
            lock (cacheSync)
            {
                if (inFlight.TryGetValue(cacheKey, out var current) &&
                    ReferenceEquals(current, pending))
                {
                    inFlight.Remove(cacheKey);
                }
                sourceBudget.ActiveResolutions = Math.Max(
                    0,
                    sourceBudget.ActiveResolutions - 1);
                pending.WorkCompleted = true;
                dispose = TryClaimPendingDispose(pending);
            }
            resolutionSlots.Release();
            if (dispose)
            {
                pending.Cancellation.Dispose();
            }
        }
    }

    private void ReleaseWaiter(PendingResolution pending)
    {
        var cancel = false;
        var dispose = false;
        lock (cacheSync)
        {
            pending.WaiterCount = Math.Max(0, pending.WaiterCount - 1);
            if (pending.WaiterCount == 0 && !pending.Completion.Task.IsCompleted)
            {
                pending.AcceptingWaiters = false;
                if (inFlight.TryGetValue(pending.CacheKey, out var current) &&
                    ReferenceEquals(current, pending))
                {
                    inFlight.Remove(pending.CacheKey);
                }
                if (!pending.CancelPending)
                {
                    pending.CancelPending = true;
                    cancel = true;
                }
            }
            dispose = TryClaimPendingDispose(pending);
        }
        if (cancel)
        {
            CompletePendingCancellation(pending);
            return;
        }
        if (dispose)
        {
            pending.Cancellation.Dispose();
        }
    }

    private void CompletePendingCancellation(PendingResolution pending)
    {
        var dispose = false;
        try
        {
            pending.Cancellation.Cancel();
        }
        finally
        {
            lock (cacheSync)
            {
                pending.CancelCompleted = true;
                dispose = TryClaimPendingDispose(pending);
            }
        }
        if (dispose)
        {
            pending.Cancellation.Dispose();
        }
    }

    private static bool TryClaimPendingDispose(PendingResolution pending)
    {
        if (pending.Disposed || !pending.WorkCompleted || pending.WaiterCount != 0 ||
            (pending.CancelPending && !pending.CancelCompleted))
        {
            return false;
        }
        pending.Disposed = true;
        return true;
    }

    private bool OwnsResolution(
        string cacheKey,
        long generation,
        PendingResolution pending) =>
        cacheGeneration == generation &&
        inFlight.TryGetValue(cacheKey, out var current) &&
        ReferenceEquals(current, pending);

    private async Task<ResolvedRemoteStream> ResolveCoreAsync(
        string sourceUrl,
        string userAgent,
        CancellationToken cancellationToken)
    {
        var sourceUri = new Uri(sourceUrl, UriKind.Absolute);

        using var request = CreateRangeRequest(sourceUri, userAgent);

        using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);
        if (IsTransientSourceFailure(response.StatusCode))
        {
            throw new RemoteSourceUnavailableException(GetSourceRetryAfterSeconds(response));
        }
        if (!IsRedirect(response.StatusCode) || response.Headers.Location is null)
        {
            throw new InvalidOperationException(
                "The STRM origin did not return a supported HTTP redirect.");
        }

        var destinationUri = response.Headers.Location.IsAbsoluteUri
            ? response.Headers.Location
            : new Uri(sourceUri, response.Headers.Location);
        var destination = destinationUri.OriginalString;
        if (!RemoteMediaSourcePolicy.TryCanonicalizeHttpUrl(destination, out var canonicalDestination))
        {
            throw new InvalidOperationException(
                "The STRM origin returned an unsafe redirect target.");
        }

        if (SharesSourceCredential(sourceUri, destinationUri))
        {
            throw new InvalidOperationException(
                "The STRM origin redirected with an original long-lived credential.");
        }

        return new ResolvedRemoteStream(canonicalDestination!);
    }

    private static HttpRequestMessage CreateRangeRequest(Uri uri, string userAgent)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Range = new RangeHeaderValue(0, 0);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        if (userAgent.Length > 0)
        {
            request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        }
        return request;
    }

    private void AddCacheEntry(
        string cacheKey,
        ResolvedRemoteStream stream,
        DateTimeOffset now)
    {
        var leaseValidUntilUtc = now.Add(CacheLeaseLifetime);

        RemoveExpiredCacheEntries(now);
        while (cache.Count >= MaximumCacheEntries)
        {
            string? oldestKey = null;
            var oldestAccess = DateTimeOffset.MaxValue;
            foreach (var candidate in cache)
            {
                if (candidate.Value.LastAccessUtc < oldestAccess)
                {
                    oldestKey = candidate.Key;
                    oldestAccess = candidate.Value.LastAccessUtc;
                }
            }
            if (oldestKey is null)
            {
                break;
            }
            cache.Remove(oldestKey);
        }
        cache[cacheKey] = new RedirectCacheEntry(
            stream,
            leaseValidUntilUtc,
            now);
    }

    private void AddFailureEntry(
        string cacheKey,
        DateTimeOffset now,
        int retryAfterSeconds)
    {
        var boundedSeconds = Math.Max(
            1,
            Math.Min((int)MaximumSourceRetryDelay.TotalSeconds, retryAfterSeconds));
        RemoveExpiredFailureEntries(now);
        var retryAfterUtc = now.AddSeconds(boundedSeconds);
        if (failures.TryGetValue(cacheKey, out var existing))
        {
            existing.LastAccessUtc = now;
            if (existing.RetryAfterUtc < retryAfterUtc)
            {
                failures[cacheKey] = new SourceFailureEntry(retryAfterUtc, now);
            }
            return;
        }
        while (failures.Count >= MaximumCacheEntries)
        {
            string? oldestKey = null;
            var oldestAccess = DateTimeOffset.MaxValue;
            foreach (var candidate in failures)
            {
                if (candidate.Value.LastAccessUtc < oldestAccess)
                {
                    oldestKey = candidate.Key;
                    oldestAccess = candidate.Value.LastAccessUtc;
                }
            }
            if (oldestKey is null)
            {
                break;
            }
            failures.Remove(oldestKey);
        }
        failures[cacheKey] = new SourceFailureEntry(
            retryAfterUtc,
            now);
    }

    private SourceResolutionBudget GetSourceBudget(string sourceKey, DateTimeOffset now)
    {
        if (sourceBudgets.TryGetValue(sourceKey, out var existing))
        {
            existing.LastAccessUtc = now;
            return existing;
        }

        while (sourceBudgets.Count >= MaximumBudgetEntries)
        {
            string? oldestKey = null;
            var oldestAccess = DateTimeOffset.MaxValue;
            foreach (var candidate in sourceBudgets)
            {
                if (candidate.Value.ActiveResolutions == 0 &&
                    candidate.Value.LastAccessUtc < oldestAccess)
                {
                    oldestKey = candidate.Key;
                    oldestAccess = candidate.Value.LastAccessUtc;
                }
            }
            if (oldestKey is null)
            {
                throw new RemoteResolutionThrottledException(5);
            }
            sourceBudgets.Remove(oldestKey);
        }

        var created = new SourceResolutionBudget(now);
        sourceBudgets.Add(sourceKey, created);
        return created;
    }

    public int RemoveExpired(DateTimeOffset now)
    {
        lock (cacheSync)
        {
            return RemoveExpiredCacheEntries(now) +
                RemoveExpiredFailureEntries(now) +
                RemoveIdleSourceBudgets(now);
        }
    }

    private int RemoveExpiredCacheEntries(DateTimeOffset now)
    {
        List<string>? expired = null;
        foreach (var candidate in cache)
        {
            if (candidate.Value.LeaseValidUntilUtc <= now)
            {
                expired ??= new List<string>();
                expired.Add(candidate.Key);
            }
        }
        if (expired is null)
        {
            return 0;
        }
        foreach (var cacheKey in expired)
        {
            cache.Remove(cacheKey);
        }
        return expired.Count;
    }

    private int RemoveExpiredFailureEntries(DateTimeOffset now)
    {
        List<string>? expired = null;
        foreach (var candidate in failures)
        {
            if (candidate.Value.RetryAfterUtc <= now)
            {
                expired ??= new List<string>();
                expired.Add(candidate.Key);
            }
        }
        if (expired is null)
        {
            return 0;
        }
        foreach (var cacheKey in expired)
        {
            failures.Remove(cacheKey);
        }
        return expired.Count;
    }

    private int RemoveIdleSourceBudgets(DateTimeOffset now)
    {
        List<string>? expired = null;
        foreach (var candidate in sourceBudgets)
        {
            if (candidate.Value.ActiveResolutions == 0 &&
                candidate.Value.LastAccessUtc.Add(SourceBudgetIdleLifetime) <= now)
            {
                expired ??= new List<string>();
                expired.Add(candidate.Key);
            }
        }
        if (expired is null)
        {
            return 0;
        }
        foreach (var sourceKey in expired)
        {
            sourceBudgets.Remove(sourceKey);
        }
        return expired.Count;
    }

    private static string NormalizeUserAgent(string? userAgent) =>
        !string.IsNullOrWhiteSpace(userAgent) && userAgent.Length <= 1024 &&
        userAgent.IndexOf('\r') < 0 && userAgent.IndexOf('\n') < 0
            ? userAgent
            : string.Empty;

    private static string CreateCacheKey(string sourceUrl, string userAgent)
    {
        using var sha256 = SHA256.Create();
        var value = Encoding.UTF8.GetBytes(sourceUrl + "\n" + userAgent);
        return Convert.ToBase64String(sha256.ComputeHash(value));
    }

    private static async Task<ResolvedRemoteStream> AwaitWithCancellationAsync(
        Task<ResolvedRemoteStream> resolution,
        CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled)
        {
            return await resolution.ConfigureAwait(false);
        }

        var canceled = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using (cancellationToken.Register(() => canceled.TrySetResult(true)))
        {
            var completed = await Task.WhenAny(resolution, canceled.Task).ConfigureAwait(false);
            if (!ReferenceEquals(completed, resolution))
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }
        return await resolution.ConfigureAwait(false);
    }

    private static void ObserveFault(Task<ResolvedRemoteStream> task)
    {
        _ = task.ContinueWith(
            failed => { _ = failed.Exception; },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static HttpClient CreateClient()
    {
        return new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
        }, true)
        {
            Timeout = SourceRequestTimeout,
        };
    }

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.MovedPermanently ||
        statusCode == HttpStatusCode.Redirect ||
        statusCode == HttpStatusCode.SeeOther ||
        statusCode == HttpStatusCode.TemporaryRedirect ||
        (int)statusCode == 308;

    private static bool IsTransientSourceFailure(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.RequestTimeout ||
        (int)statusCode == 429 ||
        (int)statusCode >= 500;

    private static RemoteSourceUnavailableException? CreateSourceUnavailableException(
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is RemoteSourceUnavailableException unavailable)
        {
            return unavailable;
        }
        if (exception is HttpRequestException ||
            (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return new RemoteSourceUnavailableException(
                (int)TransientFailureRetryDelay.TotalSeconds,
                exception);
        }
        return null;
    }

    private static int GetSourceRetryAfterSeconds(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is TimeSpan delta)
        {
            return BoundRetryAfter(delta);
        }
        if (retryAfter?.Date is DateTimeOffset date)
        {
            return BoundRetryAfter(date - DateTimeOffset.UtcNow);
        }
        return (int)TransientFailureRetryDelay.TotalSeconds;
    }

    private static int BoundRetryAfter(TimeSpan delay)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(delay.TotalSeconds));
        return Math.Min((int)MaximumSourceRetryDelay.TotalSeconds, seconds);
    }

    private static int RemainingRetrySeconds(
        DateTimeOffset retryAfterUtc,
        DateTimeOffset now) =>
        Math.Max(1, (int)Math.Ceiling((retryAfterUtc - now).TotalSeconds));

    private static bool SharesSourceCredential(Uri source, Uri destination)
    {
        foreach (var sourcePair in ParseQuery(source.Query))
        {
            if (!IsSensitiveQueryName(sourcePair.Key) ||
                string.IsNullOrEmpty(sourcePair.Value))
            {
                continue;
            }
            foreach (var destinationPair in ParseQuery(destination.Query))
            {
                if (string.Equals(
                        destinationPair.Value,
                        sourcePair.Value,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            if (DestinationContainsCredential(destination, sourcePair.Value))
            {
                return true;
            }
        }
        return false;
    }

    private static List<KeyValuePair<string, string>> ParseQuery(string query)
    {
        var result = new List<KeyValuePair<string, string>>();
        var trimmed = query.TrimStart('?');
        foreach (var pair in trimmed.Split('&'))
        {
            if (pair.Length == 0)
            {
                continue;
            }
            var separator = pair.IndexOf('=');
            var key = separator < 0 ? pair : pair.Substring(0, separator);
            var value = separator < 0 ? string.Empty : pair.Substring(separator + 1);
            try
            {
                result.Add(new KeyValuePair<string, string>(
                    Uri.UnescapeDataString(key),
                    Uri.UnescapeDataString(value)));
            }
            catch (UriFormatException)
            {
                return new List<KeyValuePair<string, string>>();
            }
        }
        return result;
    }

    private static bool IsSensitiveQueryName(string name)
    {
        var normalized = name.Replace("-", string.Empty).Replace("_", string.Empty);
        return string.Equals(normalized, "sig", StringComparison.OrdinalIgnoreCase) ||
            normalized.IndexOf("sign", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.EndsWith("key", StringComparison.OrdinalIgnoreCase) ||
            normalized.IndexOf("auth", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("credential", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("secret", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("pass", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("policy", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool DestinationContainsCredential(Uri destination, string expectedValue)
    {
        foreach (var label in destination.IdnHost.Split('.'))
        {
            if (string.Equals(label, expectedValue, StringComparison.OrdinalIgnoreCase) ||
                (expectedValue.Length >= 8 &&
                 label.IndexOf(expectedValue, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }
        }
        try
        {
            var decodedPath = Uri.UnescapeDataString(destination.AbsolutePath);
            foreach (var segment in decodedPath.Split('/'))
            {
                if (string.Equals(segment, expectedValue, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return expectedValue.Length >= 8 &&
                decodedPath.IndexOf(expectedValue, StringComparison.Ordinal) >= 0;
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private sealed class RedirectCacheEntry
    {
        public RedirectCacheEntry(
            ResolvedRemoteStream stream,
            DateTimeOffset leaseValidUntilUtc,
            DateTimeOffset lastAccessUtc)
        {
            Stream = stream;
            LeaseValidUntilUtc = leaseValidUntilUtc;
            LastAccessUtc = lastAccessUtc;
        }

        public ResolvedRemoteStream Stream { get; }

        public DateTimeOffset LeaseValidUntilUtc { get; }

        public DateTimeOffset LastAccessUtc { get; set; }
    }

    private sealed class SourceFailureEntry
    {
        public SourceFailureEntry(DateTimeOffset retryAfterUtc, DateTimeOffset lastAccessUtc)
        {
            RetryAfterUtc = retryAfterUtc;
            LastAccessUtc = lastAccessUtc;
        }

        public DateTimeOffset RetryAfterUtc { get; }

        public DateTimeOffset LastAccessUtc { get; set; }
    }

    private sealed class SourceResolutionBudget
    {
        public SourceResolutionBudget(DateTimeOffset now)
        {
            Tokens = SourceRequestBurst;
            LastRefillUtc = now;
            LastAccessUtc = now;
        }

        public int ActiveResolutions { get; set; }

        public double Tokens { get; private set; }

        public DateTimeOffset LastRefillUtc { get; private set; }

        public DateTimeOffset LastAccessUtc { get; set; }

        public int TryConsume(DateTimeOffset now)
        {
            var elapsedMinutes = Math.Max(0, (now - LastRefillUtc).TotalMinutes);
            Tokens = Math.Min(
                SourceRequestBurst,
                Tokens + elapsedMinutes * SourceRequestsPerMinute);
            LastRefillUtc = now;
            LastAccessUtc = now;
            if (Tokens >= 1)
            {
                Tokens -= 1;
                return 0;
            }

            var seconds = (1 - Tokens) * 60 / SourceRequestsPerMinute;
            return Math.Max(1, (int)Math.Ceiling(seconds));
        }
    }

    private sealed class PendingResolution
    {
        public PendingResolution(string cacheKey)
        {
            CacheKey = cacheKey;
            Cancellation = new CancellationTokenSource();
            Completion = new TaskCompletionSource<ResolvedRemoteStream>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public string CacheKey { get; }

        public CancellationTokenSource Cancellation { get; }

        public TaskCompletionSource<ResolvedRemoteStream> Completion { get; }

        public bool AcceptingWaiters { get; set; } = true;

        public bool CancelPending { get; set; }

        public bool CancelCompleted { get; set; }

        public bool WorkCompleted { get; set; }

        public bool Disposed { get; set; }

        public int WaiterCount { get; set; }
    }

}
