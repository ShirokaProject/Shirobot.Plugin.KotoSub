using System.Text.Json.Serialization;

namespace Shirobot.Plugin.KotoSub.Infrastructure.Storage;


internal sealed class RuntimeDocument
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 4;

    [JsonPropertyName("targets")]
    public Dictionary<string, PersistentTargetCheckpoint>? Targets { get; set; }

    [JsonPropertyName("feeds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, PersistentTargetCheckpoint>? LegacyFeeds { get; set; }

    [JsonPropertyName("deliveryReceipts")]
    public Dictionary<string, Dictionary<string, List<string>>> DeliveryReceipts { get; set; } = new();
}
