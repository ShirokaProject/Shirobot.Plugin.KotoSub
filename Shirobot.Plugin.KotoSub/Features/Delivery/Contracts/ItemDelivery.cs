namespace Shirobot.Plugin.KotoSub.Features.Delivery.Contracts;

internal sealed record DeliveryResult(
    bool FeedFound,
    bool FetchSucceeded,
    int FetchedItems,
    int SelectedItems,
    int DeliveredItems);

/// <summary>On-demand fetch, item selection and delivery to one subscriber (latest / test).</summary>
internal sealed class ItemDelivery(OnDemandFetch fetch, TargetRegistry feeds, ItemDispatcher dispatcher)
{
    public async Task<DeliveryResult> FetchAndDeliverAsync(
        string feedId,
        string alias,
        string trigger,
        SubscriberKey subscriber,
        Func<IReadOnlyList<UpdateEvent>, IReadOnlyList<UpdateEvent>> selectItems,
        string? replyToMessageId,
        bool includeImage,
        CancellationToken cancellationToken)
    {
        if (!feeds.TryGet(feedId, out _))
            return new DeliveryResult(false, false, 0, 0, 0);

        var fetched = await fetch.FetchFeedAsync(feedId, cancellationToken).ConfigureAwait(false);
        if (fetched is null)
            return new DeliveryResult(true, false, 0, 0, 0);

        if (!feeds.TryGet(feedId, out var feed))
            return new DeliveryResult(false, true, fetched.Items.Count, 0, 0);

        var selected = selectItems(fetched.Items);
        var delivered = 0;
        foreach (var item in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await dispatcher.SendItemAsync(subscriber, alias, feed, item, replyToMessageId,
                    includeImage, cancellationToken).ConfigureAwait(false))
                delivered++;
        }

        BotLog.Info($"[KotoSub] 手动流程完成 trigger={trigger} feed={feedId} fetched={fetched.Items.Count} " +
                    $"selected={selected.Count} delivered={delivered} target={subscriber.Format()}");
        return new DeliveryResult(true, true, fetched.Items.Count, selected.Count, delivered);
    }
}
