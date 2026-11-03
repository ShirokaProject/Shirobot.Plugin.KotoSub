namespace Shirobot.Plugin.KotoSub.Features.Subscriptions.Contracts;

internal readonly record struct Subscription(string Alias, string FeedId);

internal readonly record struct Subscriber(SubscriberKey Key, string Alias);

internal enum AddStatus
{
    Added,
    AlreadySubscribed,
    AliasTaken
}

internal readonly record struct AddOutcome(AddStatus Status, string Alias);

internal sealed record SubscriptionSnapshot(
    Dictionary<string, Dictionary<string, string>> Groups,
    Dictionary<string, Dictionary<string, string>> Friends);

/// <summary>
/// Every subscriber (group or private chat on one adapter instance) owns a private
/// alias → feed map. Aliases are only meaningful inside that map, so one subscriber can
/// neither collide with nor discover another subscriber's subscriptions.
/// </summary>
internal sealed class SubscriptionRegistry
{
    private readonly Dictionary<string, Dictionary<string, string>> _groupSubs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, string>> _friendSubs = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    public void LoadFrom(
        IReadOnlyDictionary<string, Dictionary<string, string>> groupSubs,
        IReadOnlyDictionary<string, Dictionary<string, string>> friendSubs)
    {
        lock (_lock)
        {
            _groupSubs.Clear();
            _friendSubs.Clear();
            Load(_groupSubs, SubscriberScope.Group, groupSubs);
            Load(_friendSubs, SubscriberScope.Friend, friendSubs);
        }
    }

    public SubscriptionSnapshot Snapshot()
    {
        lock (_lock) return new SubscriptionSnapshot(Copy(_groupSubs), Copy(_friendSubs));
    }

    public AddOutcome Add(SubscriberKey key, string alias, string feedId)
    {
        lock (_lock)
        {
            var bucket = GetBucket(key, create: true)!;
            foreach (var (existingAlias, existingFeed) in bucket)
            {
                if (string.Equals(existingFeed, feedId, StringComparison.OrdinalIgnoreCase))
                    return new AddOutcome(AddStatus.AlreadySubscribed, existingAlias);
            }

            if (bucket.ContainsKey(alias))
                return new AddOutcome(AddStatus.AliasTaken, alias);

            bucket[alias] = feedId;
            return new AddOutcome(AddStatus.Added, alias);
        }
    }

    public bool TryGetFeed(SubscriberKey key, string alias, out string feedId)
    {
        lock (_lock)
        {
            var bucket = GetBucket(key, create: false);
            if (bucket is not null && bucket.TryGetValue(alias, out var found))
            {
                feedId = found;
                return true;
            }

            feedId = string.Empty;
            return false;
        }
    }

    public IReadOnlyList<Subscription> List(SubscriberKey key)
    {
        lock (_lock)
        {
            var bucket = GetBucket(key, create: false);
            if (bucket is null) return [];
            return bucket
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new Subscription(pair.Key, pair.Value))
                .ToList();
        }
    }

    /// <summary>Removes one alias and returns the feed it pointed to, or null if the subscriber has no such alias.</summary>
    public string? Remove(SubscriberKey key, string alias)
    {
        lock (_lock)
        {
            var dict = DictionaryFor(key.Scope);
            if (!dict.TryGetValue(key.StorageKey, out var bucket) || !bucket.Remove(alias, out var feedId))
                return null;

            if (bucket.Count == 0) dict.Remove(key.StorageKey);
            return feedId;
        }
    }

    public bool RenameAlias(SubscriberKey key, string oldAlias, string newAlias)
    {
        lock (_lock)
        {
            var bucket = GetBucket(key, create: false);
            if (bucket is null || bucket.ContainsKey(newAlias) || !bucket.Remove(oldAlias, out var feedId))
                return false;

            bucket[newAlias] = feedId;
            return true;
        }
    }

    public IReadOnlyList<Subscriber> SubscribersOf(string feedId)
    {
        lock (_lock)
        {
            var subscribers = new List<Subscriber>();
            Collect(_groupSubs, SubscriberScope.Group, feedId, subscribers);
            Collect(_friendSubs, SubscriberScope.Friend, feedId, subscribers);
            return subscribers;
        }
    }

    /// <summary>
    /// Moves a pre-instance ("platform|target") bucket into the given instance-bound bucket. Callers must only
    /// do this when the platform has exactly one adapter instance, so ownership is unambiguous.
    /// </summary>
    public bool AdoptLegacy(SubscriberKey key)
    {
        if (key.InstanceId is null) return false;
        lock (_lock)
        {
            var dict = DictionaryFor(key.Scope);
            if (!dict.Remove(key.LegacyStorageKey, out var legacy)) return false;

            var bucket = GetBucket(key, create: true)!;
            foreach (var (alias, feedId) in legacy)
            {
                if (HasFeed(bucket, feedId)) continue;
                bucket[UniqueAlias(bucket, alias)] = feedId;
            }

            return true;
        }
    }

    private static bool HasFeed(Dictionary<string, string> bucket, string feedId) =>
        bucket.Values.Any(candidate => string.Equals(candidate, feedId, StringComparison.OrdinalIgnoreCase));

    private static string UniqueAlias(Dictionary<string, string> bucket, string alias)
    {
        if (!bucket.ContainsKey(alias)) return alias;
        for (var i = 2; ; i++)
        {
            var candidate = $"{alias}-{i}";
            if (!bucket.ContainsKey(candidate)) return candidate;
        }
    }

    private static void Collect(
        Dictionary<string, Dictionary<string, string>> source, SubscriberScope scope, string feedId,
        List<Subscriber> into)
    {
        foreach (var (storageKey, bucket) in source)
        {
            foreach (var (alias, id) in bucket)
            {
                if (string.Equals(id, feedId, StringComparison.OrdinalIgnoreCase))
                    into.Add(new Subscriber(SubscriberKey.FromStorageKey(scope, storageKey), alias));
            }
        }
    }

    private static void Load(
        Dictionary<string, Dictionary<string, string>> target, SubscriberScope scope,
        IReadOnlyDictionary<string, Dictionary<string, string>> source)
    {
        foreach (var (key, aliases) in source)
        {
            if (string.IsNullOrWhiteSpace(key) || aliases is null || aliases.Count == 0) continue;
            target[SubscriberKey.FromStorageKey(scope, key).StorageKey] =
                new Dictionary<string, string>(aliases, StringComparer.OrdinalIgnoreCase);
        }
    }

    private static Dictionary<string, Dictionary<string, string>> Copy(
        Dictionary<string, Dictionary<string, string>> source) =>
        source.ToDictionary(
            pair => pair.Key,
            pair => pair.Value
                .OrderBy(alias => alias.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(alias => alias.Key, alias => alias.Value));

    private Dictionary<string, Dictionary<string, string>> DictionaryFor(SubscriberScope scope) =>
        scope == SubscriberScope.Group ? _groupSubs : _friendSubs;

    private Dictionary<string, string>? GetBucket(SubscriberKey key, bool create)
    {
        var dict = DictionaryFor(key.Scope);
        if (dict.TryGetValue(key.StorageKey, out var bucket)) return bucket;
        if (!create) return null;

        bucket = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        dict[key.StorageKey] = bucket;
        return bucket;
    }
}
