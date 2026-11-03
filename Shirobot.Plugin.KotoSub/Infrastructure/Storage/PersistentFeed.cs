using System.Text.Json.Serialization;

namespace Shirobot.Plugin.KotoSub.Infrastructure.Storage;


internal sealed class PersistentFeed
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string SourceKind { get; set; } = "rss";

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("feedImageUrl")]
    public string? FeedImageUrl { get; set; }

    [JsonPropertyName("generator")]
    public string? Generator { get; set; }

    [JsonPropertyName("intervalSec")]
    public int? IntervalSeconds { get; set; }

    [JsonPropertyName("lastSeen")]
    public List<string> LastSeenGuids { get; set; } = new();

    [JsonPropertyName("lastFetchAt")]
    public DateTimeOffset? LastFetchAt { get; set; }

    [JsonPropertyName("etag")]
    public string? ETag { get; set; }

    [JsonPropertyName("lastModified")]
    public DateTimeOffset? LastModified { get; set; }

    [JsonPropertyName("consecutiveFailures")]
    public int ConsecutiveFailures { get; set; }

    [JsonPropertyName("createdBy")]
    public string? CreatedBy { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
}
