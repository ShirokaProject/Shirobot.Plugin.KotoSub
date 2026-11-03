namespace Shirobot.Plugin.KotoSub.Features.Sources.Contracts;

/// <summary>Records provider-neutral update progress and provider-owned checkpoints for a target.</summary>
internal sealed class TargetProgress(TargetRegistry feeds)
{
    public void MarkSeen(string id, IEnumerable<string> guids, int capacity) =>
        feeds.Update(id, feed =>
        {
            feed.LastCheckedAt = DateTimeOffset.UtcNow;
            feed.ConsecutiveFailures = 0;
            foreach (var guid in guids)
            {
                if (!feed.SeenUpdateIds.Contains(guid)) feed.SeenUpdateIds.Add(guid);
            }

            if (capacity > 0 && feed.SeenUpdateIds.Count > capacity)
                feed.SeenUpdateIds.RemoveRange(0, feed.SeenUpdateIds.Count - capacity);
        });

    public void RecordFailure(string id) =>
        feeds.Update(id, feed =>
        {
            feed.LastCheckedAt = DateTimeOffset.UtcNow;
            feed.ConsecutiveFailures++;
        });

    public void SetValidators(string id, string? etag, DateTimeOffset? lastModified) =>
        feeds.Update(id, feed =>
        {
            feed.ETag = etag;
            feed.LastModified = lastModified;
        });

    public void SetInterval(string id, int? seconds) => feeds.Update(id, feed => feed.IntervalSeconds = seconds);

    public void ApplyMetadata(string id, SourceUpdateBatch result) =>
        feeds.Update(id, feed =>
        {
            if (!string.IsNullOrWhiteSpace(result.SourceName)) feed.DisplayName = result.SourceName.Trim();
            foreach (var (key, value) in result.Metadata)
                feed.Metadata[key] = value;
            if (Uri.TryCreate(result.SourceImageUrl, UriKind.Absolute, out var image) &&
                image.Scheme is "http" or "https")
                feed.SourceImageUrl = image.AbsoluteUri;
            if (result.ProviderState.Count > 0)
                feed.ProviderState = new(result.ProviderState, StringComparer.Ordinal);
        });
}
