namespace Shirobot.Plugin.KotoSub.Features.Sources.Contracts;

internal sealed class FeedSource
{
    public string Id { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    /// <summary>Which source reads it, e.g. "rss".</summary>
    public string SourceKind { get; set; } = "rss";
    public string? DisplayName { get; set; }
    public string? FeedImageUrl { get; set; }
    public string? Generator { get; set; }
    public int? IntervalSeconds { get; set; }
    public List<string> LastSeenGuids { get; set; } = new();
    public DateTimeOffset? LastFetchAt { get; set; }
    public string? ETag { get; set; }
    public DateTimeOffset? LastModified { get; set; }
    public int ConsecutiveFailures { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string DisplayOrHost => !string.IsNullOrWhiteSpace(DisplayName) ? DisplayName! : FeedUrl.HostOf(Url);
}
