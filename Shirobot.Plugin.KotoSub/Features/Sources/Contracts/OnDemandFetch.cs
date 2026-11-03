using System.Diagnostics;

namespace Shirobot.Plugin.KotoSub.Features.Sources.Contracts;

/// <summary>Fetches on request (adding a subscription, "latest", "test") outside the polling schedule.</summary>
internal sealed class OnDemandFetch(FeedRegistry feeds, FeedProgress progress, SourceCatalog sources)
{
    public Task<FeedFetchResult> FetchNewAsync(ISubscriptionSource source, string url, CancellationToken cancellationToken) =>
        source.FetchAsync(new FetchRequest(url), cancellationToken);

    /// <summary>Fetches a known feed; null when it is unknown or the fetch failed.</summary>
    public async Task<FeedFetchResult?> FetchFeedAsync(string feedId, CancellationToken cancellationToken)
    {
        if (!feeds.TryGet(feedId, out var feed)) return null;

        var elapsed = Stopwatch.StartNew();
        try
        {
            var result = await sources.ForFeed(feed).FetchAsync(new FetchRequest(feed.Url), cancellationToken);
            progress.ApplyMetadata(feedId, result);
            BotLog.Info($"[KotoSub] 手动抓取成功 feed={feedId} host={FeedUrl.HostOf(feed.Url)} " +
                        $"items={result.Items.Count} elapsed_ms={elapsed.ElapsedMilliseconds}");
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            BotLog.Warning($"[KotoSub] 手动抓取失败 feed={feedId} host={FeedUrl.HostOf(feed.Url)} " +
                           $"elapsed_ms={elapsed.ElapsedMilliseconds} error={FetchLog.Describe(ex)}");
            return null;
        }
    }
}
