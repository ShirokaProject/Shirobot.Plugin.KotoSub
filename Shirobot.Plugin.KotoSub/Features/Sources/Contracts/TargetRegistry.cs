using Shirobot.Plugin.KotoSub.Features.Sources.Rss;

namespace Shirobot.Plugin.KotoSub.Features.Sources.Contracts;

/// <summary>Known subscription targets, keyed by opaque internal target id.</summary>
internal sealed class TargetRegistry
{
    private readonly Dictionary<string, SubscriptionTarget> _targets = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public void LoadFrom(IReadOnlyDictionary<string, PersistentTarget> persisted)
    {
        lock (_lock)
        {
            _targets.Clear();
            foreach (var (id, saved) in persisted)
            {
                _targets[id] = new SubscriptionTarget
                {
                    Id = id,
                    Key = saved.Key,
                    ProviderId = saved.ProviderId,
                    DisplayName = saved.DisplayName,
                    SourceImageUrl = saved.SourceImageUrl ?? saved.LegacySourceImageUrl,
                    Metadata = MergeMetadata(saved.Metadata, saved.LegacyGenerator),
                    IntervalSeconds = saved.IntervalSeconds,
                    SeenUpdateIds = (saved.SeenUpdateIds ?? saved.LegacySeenUpdateIds)?.ToList() ?? [],
                    LastCheckedAt = saved.LastCheckedAt ?? saved.LegacyLastCheckedAt,
                    ETag = saved.ETag,
                    LastModified = saved.LastModified,
                    ConsecutiveFailures = saved.ConsecutiveFailures,
                    ProviderState = new(saved.ProviderState, StringComparer.Ordinal),
                    CreatedBy = saved.CreatedBy,
                    CreatedAt = saved.CreatedAt == default ? DateTimeOffset.UtcNow : saved.CreatedAt
                };
            }
        }
    }

    public Dictionary<string, PersistentTarget> Snapshot()
    {
        lock (_lock)
        {
            return _targets.ToDictionary(pair => pair.Key, pair => new PersistentTarget
            {
                Key = pair.Value.Key,
                ProviderId = pair.Value.ProviderId,
                DisplayName = pair.Value.DisplayName,
                SourceImageUrl = pair.Value.SourceImageUrl,
                Metadata = new(pair.Value.Metadata, StringComparer.OrdinalIgnoreCase),
                IntervalSeconds = pair.Value.IntervalSeconds,
                SeenUpdateIds = pair.Value.SeenUpdateIds.ToList(),
                LastCheckedAt = pair.Value.LastCheckedAt,
                ETag = pair.Value.ETag,
                LastModified = pair.Value.LastModified,
                ConsecutiveFailures = pair.Value.ConsecutiveFailures,
                ProviderState = new(pair.Value.ProviderState, StringComparer.Ordinal),
                CreatedBy = pair.Value.CreatedBy,
                CreatedAt = pair.Value.CreatedAt
            });
        }
    }

    public bool TryGet(string id, out SubscriptionTarget target)
    {
        lock (_lock)
        {
            if (_targets.TryGetValue(id, out var found))
            {
                target = found;
                return true;
            }

            target = null!;
            return false;
        }
    }

    public IReadOnlyList<SubscriptionTarget> All()
    {
        lock (_lock) return _targets.Values.ToList();
    }

    public SubscriptionTarget? Find(string providerId, string key)
    {
        lock (_lock) return _targets.Values.FirstOrDefault(target =>
            target.ProviderId.Equals(providerId, StringComparison.OrdinalIgnoreCase) &&
            (providerId.Equals(RssSource.SourceId, StringComparison.OrdinalIgnoreCase)
                ? FeedUrl.Same(target.Key, key)
                : string.Equals(target.Key, key, StringComparison.Ordinal)));
    }

    /// <summary>Registers a target after its current state has been recorded as the initial baseline.</summary>
    public bool TryAddInitialized(
        string id, string providerId, string key, string? displayName, string? createdBy,
        IReadOnlyList<UpdateEvent> baselineItems, int seenCapacity, out SubscriptionTarget target,
        Action<SubscriptionTarget>? onAdded = null, string? sourceImageUrl = null, IReadOnlyDictionary<string, string>? metadata = null)
    {
        lock (_lock)
        {
            var existing = _targets.TryGetValue(id, out var byId) ? byId : Find(providerId, key);
            if (existing is not null)
            {
                target = existing;
                return false;
            }

            var seenIds = UpdateEvent.SeenOrder(baselineItems).ToList();
            if (seenCapacity > 0 && seenIds.Count > seenCapacity)
                seenIds = seenIds.TakeLast(seenCapacity).ToList();

            target = new SubscriptionTarget
            {
                Id = id,
                ProviderId = providerId,
                Key = key,
                DisplayName = displayName,
                SourceImageUrl = sourceImageUrl,
                Metadata = metadata is null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase),
                SeenUpdateIds = seenIds,
                LastCheckedAt = DateTimeOffset.UtcNow,
                CreatedBy = createdBy,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _targets[id] = target;
            try
            {
                onAdded?.Invoke(target);
            }
            catch
            {
                _targets.Remove(id);
                throw;
            }

            return true;
        }
    }

    public bool Remove(string id)
    {
        lock (_lock) return _targets.Remove(id);
    }

    public bool Update(string id, Action<SubscriptionTarget> mutate)
    {
        lock (_lock)
        {
            if (!_targets.TryGetValue(id, out var target)) return false;
            mutate(target);
            return true;
        }
    }

    private static Dictionary<string, string> MergeMetadata(IReadOnlyDictionary<string, string>? metadata, string? legacyGenerator)
    {
        var result = metadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(legacyGenerator)) result["generator"] = legacyGenerator;
        return result;
    }
}
