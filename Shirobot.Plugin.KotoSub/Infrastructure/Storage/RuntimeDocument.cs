using System.Text.Json.Serialization;

namespace Shirobot.Plugin.KotoSub.Infrastructure.Storage;


internal sealed class RuntimeDocument
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 3;

    [JsonPropertyName("feeds")]
    public Dictionary<string, PersistentFeedCheckpoint> Feeds { get; set; } = new();

    [JsonPropertyName("deliveryReceipts")]
    public Dictionary<string, Dictionary<string, List<string>>> DeliveryReceipts { get; set; } = new();
}
