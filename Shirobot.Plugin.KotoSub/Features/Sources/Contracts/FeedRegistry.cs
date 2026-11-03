namespace Shirobot.Plugin.KotoSub.Features.Sources.Contracts;

/// <summary>All feeds known to the plugin, keyed by their opaque internal id.</summary>
internal sealed class FeedRegistry
{
    private readonly Dictionary<string, FeedSource> _feeds = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public void LoadFrom(IReadOnlyDictionary<string, PersistentFeed> persisted)
    {
        lock (_lock)
        {
            _feeds.Clear();
            foreach (var (id, saved) in persisted)
            {
                _feeds[id] = new FeedSource
                {
                    Id = id,
                    Url = saved.Url,
                    SourceKind = saved.SourceKind,
                    DisplayName = saved.DisplayName,
                    FeedImageUrl = saved.FeedImageUrl,
                    Generator = saved.Generator,
                    IntervalSeconds = saved.IntervalSeconds,
                    LastSeenGuids = saved.LastSeenGuids?.ToList() ?? [],
                    LastFetchAt = saved.LastFetchAt,
                    ETag = saved.ETag,
                    LastModified = saved.LastModified,
                    ConsecutiveFailures = saved.ConsecutiveFailures,
                    CreatedBy = saved.CreatedBy,
                    CreatedAt = saved.CreatedAt == default ? DateTimeOffset.UtcNow : saved.CreatedAt
                };
            }
        }
    }

    public Dictionary<string, PersistentFeed> Snapshot()
    {
        lock (_lock)
        {
            return _feeds.ToDictionary(pair => pair.Key, pair => new PersistentFeed
            {
                Url = pair.Value.Url,
                SourceKind = pair.Value.SourceKind,
                DisplayName = pair.Value.DisplayName,
                FeedImageUrl = pair.Value.FeedImageUrl,
                Generator = pair.Value.Generator,
                IntervalSeconds = pair.Value.IntervalSeconds,
                LastSeenGuids = pair.Value.LastSeenGuids.ToList(),
                LastFetchAt = pair.Value.LastFetchAt,
                ETag = pair.Value.ETag,
                LastModified = pair.Value.LastModified,
                ConsecutiveFailures = pair.Value.ConsecutiveFailures,
                CreatedBy = pair.Value.CreatedBy,
                CreatedAt = pair.Value.CreatedAt
            });
        }
    }

    public bool TryGet(string id, out FeedSource feed)
    {
        lock (_lock)
        {
            if (_feeds.TryGetValue(id, out var found))
            {
                feed = found;
                return true;
            }

            feed = null!;
            return false;
        }
    }

    public IReadOnlyList<FeedSource> All()
    {
        lock (_lock) return _feeds.Values.ToList();
    }

    public FeedSource? FindByUrl(string url)
    {
        lock (_lock) return _feeds.Values.FirstOrDefault(feed => FeedUrl.Same(feed.Url, url));
    }

    /// <summary>Adds a feed whose current items are already recorded as seen, unless the URL or id is taken.</summary>
    public bool TryAddInitialized(
        string id, string url, string? displayName, string? createdBy,
        IReadOnlyList<FeedItem> baselineItems, int lastSeenCapacity, out FeedSource feed,
        Action<FeedSource>? onAdded = null, string sourceKind = "rss", string? feedImageUrl = null,
        string? generator = null)
    {
        lock (_lock)
        {
            var existing = _feeds.Values.FirstOrDefault(f => FeedUrl.Same(f.Url, url));
            if (existing is not null || _feeds.TryGetValue(id, out existing))
            {
                feed = existing!;
                return false;
            }

            var guids = FeedItem.SeenOrder(baselineItems).ToList();
            if (lastSeenCapacity > 0 && guids.Count > lastSeenCapacity)
                guids = guids.TakeLast(lastSeenCapacity).ToList();

            feed = new FeedSource
            {
                Id = id,
                Url = url,
                SourceKind = sourceKind,
                DisplayName = displayName,
                FeedImageUrl = feedImageUrl,
                Generator = generator,
                LastSeenGuids = guids,
                LastFetchAt = DateTimeOffset.UtcNow,
                CreatedBy = createdBy,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _feeds[id] = feed;
            try
            {
                onAdded?.Invoke(feed);
            }
            catch
            {
                _feeds.Remove(id);
                throw;
            }

            return true;
        }
    }

    public bool Remove(string id)
    {
        lock (_lock) return _feeds.Remove(id);
    }

    /// <summary>Runs a mutation under the registry lock; returns false if the feed no longer exists.</summary>
    public bool Update(string id, Action<FeedSource> mutate)
    {
        lock (_lock)
        {
            if (!_feeds.TryGetValue(id, out var feed)) return false;
            mutate(feed);
            return true;
        }
    }
}
