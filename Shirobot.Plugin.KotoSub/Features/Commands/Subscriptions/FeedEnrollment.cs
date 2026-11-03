namespace Shirobot.Plugin.KotoSub.Features.Commands.Subscriptions;

/// <summary>Creates the shared feed entry for an address nobody followed yet.</summary>
internal sealed class FeedEnrollment(FeedRegistry feeds, Func<KotoSubConfig> config)
{
    public FeedSource? Find(string url) => feeds.FindByUrl(url);

    /// <summary>Adds the feed and runs <paramref name="subscribe"/> atomically; false means somebody else just added it.</summary>
    public bool TryEnroll(
        string url, FeedFetchResult fetched, string sourceKind, string createdBy,
        Action<FeedSource> subscribe, out FeedSource feed) =>
        feeds.TryAddInitialized(
            FeedUrl.KeyOf(url), url, fetched.FeedTitle, createdBy, fetched.Items, config().LastSeenCapacity,
            out feed, subscribe, sourceKind, fetched.FeedImageUrl, fetched.Generator);
}
