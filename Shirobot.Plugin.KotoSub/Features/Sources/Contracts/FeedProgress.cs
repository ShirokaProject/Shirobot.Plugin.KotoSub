namespace Shirobot.Plugin.KotoSub.Features.Sources.Contracts;

/// <summary>Records what a fetch learned about a feed: seen items, validators, failures, metadata.</summary>
internal sealed class FeedProgress(FeedRegistry feeds)
{
    public void MarkSeen(string id, IEnumerable<string> guids, int capacity) =>
        feeds.Update(id, feed =>
        {
            feed.LastFetchAt = DateTimeOffset.UtcNow;
            feed.ConsecutiveFailures = 0;
            foreach (var guid in guids)
            {
                if (!feed.LastSeenGuids.Contains(guid)) feed.LastSeenGuids.Add(guid);
            }

            if (capacity > 0 && feed.LastSeenGuids.Count > capacity)
                feed.LastSeenGuids.RemoveRange(0, feed.LastSeenGuids.Count - capacity);
        });

    public void RecordFailure(string id) =>
        feeds.Update(id, feed =>
        {
            feed.LastFetchAt = DateTimeOffset.UtcNow;
            feed.ConsecutiveFailures++;
        });

    public void SetValidators(string id, string? etag, DateTimeOffset? lastModified) =>
        feeds.Update(id, feed =>
        {
            feed.ETag = etag;
            feed.LastModified = lastModified;
        });

    public void SetInterval(string id, int? seconds) => feeds.Update(id, feed => feed.IntervalSeconds = seconds);

    public void ApplyMetadata(string id, FeedFetchResult result) =>
        feeds.Update(id, feed =>
        {
            if (!string.IsNullOrWhiteSpace(result.FeedTitle)) feed.DisplayName = result.FeedTitle.Trim();
            if (!string.IsNullOrWhiteSpace(result.Generator)) feed.Generator = result.Generator.Trim();
            if (Uri.TryCreate(result.FeedImageUrl, UriKind.Absolute, out var image) &&
                image.Scheme is "http" or "https")
                feed.FeedImageUrl = image.AbsoluteUri;
        });
}
