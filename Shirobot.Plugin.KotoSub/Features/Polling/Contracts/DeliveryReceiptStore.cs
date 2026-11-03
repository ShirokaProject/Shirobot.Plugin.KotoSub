namespace Shirobot.Plugin.KotoSub.Features.Polling.Contracts;

/// <summary>Remembers which target already got which item, so a retry only resends to failed targets.</summary>
internal sealed class DeliveryReceiptStore
{
    private readonly Dictionary<string, Dictionary<string, HashSet<string>>> _receipts =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public void Load(Dictionary<string, Dictionary<string, List<string>>> saved)
    {
        lock (_lock)
        {
            _receipts.Clear();
            foreach (var (feedId, items) in saved)
                _receipts[feedId] = items.ToDictionary(
                    pair => pair.Key, pair => new HashSet<string>(pair.Value ?? [], StringComparer.Ordinal),
                    StringComparer.Ordinal);
        }
    }

    /// <summary>Copy for saving; receipts of feeds that no longer exist are left out.</summary>
    public Dictionary<string, Dictionary<string, List<string>>> Snapshot(Func<string, bool> feedExists)
    {
        lock (_lock)
            return _receipts.Where(feed => feedExists(feed.Key)).ToDictionary(
                feed => feed.Key,
                feed => feed.Value.ToDictionary(item => item.Key, item => item.Value.ToList(), StringComparer.Ordinal),
                StringComparer.OrdinalIgnoreCase);
    }

    public bool Has(string feedId, string itemId, string target)
    {
        lock (_lock)
            return _receipts.TryGetValue(feedId, out var items)
                   && items.TryGetValue(itemId, out var targets) && targets.Contains(target);
    }

    public void Add(string feedId, string itemId, string target)
    {
        lock (_lock)
        {
            if (!_receipts.TryGetValue(feedId, out var items))
                _receipts[feedId] = items = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            if (!items.TryGetValue(itemId, out var targets))
                items[itemId] = targets = new HashSet<string>(StringComparer.Ordinal);
            targets.Add(target);
        }
    }

    public void ClearItem(string feedId, string itemId)
    {
        lock (_lock)
        {
            if (!_receipts.TryGetValue(feedId, out var items)) return;
            items.Remove(itemId);
            if (items.Count == 0) _receipts.Remove(feedId);
        }
    }

    public void ClearFeed(string feedId)
    {
        lock (_lock) _receipts.Remove(feedId);
    }
}
