using System.Diagnostics;

namespace Shirobot.Plugin.KotoSub.Features.Polling;

/// <summary>One full check of one feed: fetch, learn what is new, push it, save.</summary>
internal sealed class UpdateCheckRunner(
    SourceFetchStep fetch, ItemPusher pusher, BaselineTracker baseline, TargetProgress progress, StatePersister persister)
{
    public async Task RunAsync(SubscriptionTarget feed, bool baselineMode, CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.StartNew();
        var result = await fetch.FetchAsync(feed, baselineMode, cancellationToken);
        if (result is null) return;

        var items = result.Items;
        var seen = new HashSet<string>(feed.SeenUpdateIds, StringComparer.OrdinalIgnoreCase);
        var newItems = items.Where(item => !string.IsNullOrWhiteSpace(item.Id) && seen.Add(item.Id)).ToList();

        if (baselineMode || feed.LastCheckedAt is null && feed.SeenUpdateIds.Count == 0)
        {
            baseline.Establish(feed.Id, items);
            BotLog.Info($"[KotoSub] baseline 完成 feed={feed.Id} host={FeedUrl.HostOf(feed.Key)} " +
                        $"items={items.Count} elapsed_ms={elapsed.ElapsedMilliseconds}，历史条目不推送。");
        }
        else if (newItems.Count == 0)
        {
            progress.MarkSeen(feed.Id, [], 0);
        }
        else
        {
            await pusher.PushAsync(feed, newItems, elapsed.ElapsedMilliseconds);
        }

        await persister.PersistAsync();
    }
}
