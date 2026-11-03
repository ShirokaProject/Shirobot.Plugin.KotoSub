namespace Shirobot.Plugin.KotoSub.Features.Polling;

/// <summary>Sends newly seen items to every subscriber of a feed and remembers who already got them.</summary>
internal sealed class ItemPusher(
    SubscriptionRegistry subscriptions, ItemDispatcher dispatcher, DeliveryReceiptStore receipts,
    FeedProgress progress, Func<KotoSubConfig> config)
{
    public async Task PushAsync(FeedSource feed, IReadOnlyList<FeedItem> newItems, long elapsedMs)
    {
        var settings = config();
        var subscribers = subscriptions.SubscribersOf(feed.Id);
        if (subscribers.Count == 0)
        {
            progress.MarkSeen(feed.Id, newItems.Select(item => item.Id), settings.LastSeenCapacity);
            receipts.ClearFeed(feed.Id);
            return;
        }

        var pushItems = newItems
            .OrderByDescending(item => item.Published ?? DateTimeOffset.MinValue)
            .Take(Math.Max(1, settings.MaxItemsPerPush))
            .Reverse()
            .ToList();

        BotLog.Info(
            $"[KotoSub] 检测到更新 feed={feed.Id} new={newItems.Count} push={pushItems.Count} " +
            $"skipped={newItems.Count - pushItems.Count} subscribers={subscribers.Count} elapsed_ms={elapsedMs}");

        var deliveredIds = new List<string>();
        foreach (var item in pushItems)
        {
            var deliveredToAll = true;
            foreach (var (subscriber, alias) in subscribers)
            {
                var target = subscriber.Format();
                if (receipts.Has(feed.Id, item.Id, target)) continue;
                if (await dispatcher.PushItemAsync(subscriber, alias, feed, item)) receipts.Add(feed.Id, item.Id, target);
                else deliveredToAll = false;
            }

            if (!deliveredToAll) continue;
            deliveredIds.Add(item.Id);
            receipts.ClearItem(feed.Id, item.Id);
        }

        // Items beyond max_items_per_push are intentionally skipped; failed sends stay pending for a retry.
        foreach (var skipped in newItems.Except(pushItems))
        {
            deliveredIds.Add(skipped.Id);
            receipts.ClearItem(feed.Id, skipped.Id);
        }

        progress.MarkSeen(feed.Id, deliveredIds, settings.LastSeenCapacity);

        var pending = pushItems.Count(item => !deliveredIds.Contains(item.Id));
        if (pending > 0)
            BotLog.Warning($"[KotoSub] 推送未全部完成 feed={feed.Id} pending_items={pending}，下次轮询将重试失败目标。");
        else
            BotLog.Info($"[KotoSub] 推送完成 feed={feed.Id} delivered_items={pushItems.Count} subscribers={subscribers.Count}");
    }
}
