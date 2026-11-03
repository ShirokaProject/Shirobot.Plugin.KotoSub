using System.Text.Json.Serialization;

namespace Shirobot.Plugin.KotoSub.Infrastructure.Storage;


internal sealed class PersistentFeedDefinition
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string SourceKind { get; set; } = "rss";

    [JsonPropertyName("intervalSec")]
    public int? IntervalSeconds { get; set; }

    [JsonPropertyName("createdBy")]
    public string? CreatedBy { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
}
