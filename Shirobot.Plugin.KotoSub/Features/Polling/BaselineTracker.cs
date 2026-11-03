namespace Shirobot.Plugin.KotoSub.Features.Polling;

internal sealed class BaselineTracker(FeedProgress progress, DeliveryReceiptStore receipts, Func<KotoSubConfig> config)
{
    private readonly HashSet<string> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _attempted = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public void Reset(IEnumerable<string> feedIds)
    {
        lock (_lock)
        {
            _pending.Clear();
            _attempted.Clear();
            _pending.UnionWith(feedIds);
        }
    }

    public (bool Pending, bool Attempted) Status(string feedId)
    {
        lock (_lock) return (_pending.Contains(feedId), _attempted.Contains(feedId));
    }

    public void MarkAttempt(string feedId)
    {
        lock (_lock) _attempted.Add(feedId);
    }

    /// <summary>Everything currently in the feed counts as seen; nothing is pushed.</summary>
    public void Establish(string feedId, IReadOnlyList<FeedItem> items)
    {
        progress.MarkSeen(feedId, FeedItem.SeenOrder(items), config().LastSeenCapacity);
        receipts.ClearFeed(feedId);
        lock (_lock)
        {
            _pending.Remove(feedId);
            _attempted.Remove(feedId);
        }
    }
}
