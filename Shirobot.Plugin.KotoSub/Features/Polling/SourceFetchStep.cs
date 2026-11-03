using System.Diagnostics;

namespace Shirobot.Plugin.KotoSub.Features.Polling;

/// <summary>
/// Fetches one feed under the global concurrency limit. A failure or "not modified" answer is fully handled
/// here (recorded and saved) and reported as null, so callers only continue with real content.
/// </summary>
internal sealed class SourceFetchStep(
    TargetProgress progress, SourceCatalog sources, StatePersister persister, Func<KotoSubConfig> config)
    : IDisposable
{
    private readonly SemaphoreSlim _limiter = new(4, 4);

    public async Task<SourceUpdateBatch?> FetchAsync(SubscriptionTarget feed, bool baselineMode, CancellationToken cancellationToken)
    {
        var settings = config();
        var elapsed = Stopwatch.StartNew();
        var priorFailures = feed.ConsecutiveFailures;
        var firstFetch = baselineMode || feed.LastCheckedAt is null;

        SourceUpdateBatch? result = null;
        Exception? failure = null;
        await _limiter.WaitAsync(cancellationToken);
        try
        {
            var request = new FetchRequest(feed.Key,
                firstFetch ? null : feed.ETag,
                firstFetch ? null : feed.LastModified,
                feed.ProviderState);
            result = await sources.ForTarget(feed).FetchAsync(request, cancellationToken);
            if (result.NotModified && firstFetch)
                throw new InvalidOperationException("建立历史基线时收到 HTTP 304，无法读取内容。");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            _limiter.Release();
        }

        if (failure is not null || result is null)
        {
            progress.RecordFailure(feed.Id);
            var retryAt = BackoffPolicy.NextDueAt(feed, settings);
            var retryIn = Math.Max(0, (int)Math.Ceiling((retryAt - DateTimeOffset.UtcNow).TotalSeconds));
            BotLog.Warning(
                $"[KotoSub] 抓取失败 feed={feed.Id} host={FeedUrl.HostOf(feed.Key)} " +
                $"elapsed_ms={elapsed.ElapsedMilliseconds} failures={feed.ConsecutiveFailures} " +
                $"retry_in_sec={retryIn} retry_at={retryAt.ToLocalTime():yyyy-MM-dd HH:mm:ss zzz} " +
                $"error={FetchLog.Describe(failure ?? new InvalidOperationException("空响应"))}");
            await persister.PersistAsync();
            return null;
        }

        if (priorFailures > 0)
            BotLog.Info($"[KotoSub] 抓取恢复 feed={feed.Id} host={FeedUrl.HostOf(feed.Key)} " +
                        $"previous_failures={priorFailures} items={result.Items.Count} elapsed_ms={elapsed.ElapsedMilliseconds}");

        if (result.NotModified)
        {
            progress.SetValidators(feed.Id, result.ETag ?? feed.ETag, result.LastModified ?? feed.LastModified);
            progress.MarkSeen(feed.Id, [], settings.LastSeenCapacity);
            await persister.PersistAsync();
            return null;
        }

        progress.SetValidators(feed.Id, result.ETag, result.LastModified);
        progress.ApplyMetadata(feed.Id, result);
        return result;
    }

    public void Dispose() => _limiter.Dispose();
}
