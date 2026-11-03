using System.Text.Json.Serialization;

namespace Shirobot.Plugin.KotoSub.Infrastructure.Storage;


internal sealed class SubState
{
    [JsonPropertyName("targets")]
    public Dictionary<string, PersistentTarget> Targets { get; set; } = new();

    /// <summary>Subscriber storage key → (alias → internal feed id).</summary>
    [JsonPropertyName("groupSubs")]
    public Dictionary<string, Dictionary<string, string>> GroupSubs { get; set; } = new();

    [JsonPropertyName("friendSubs")]
    public Dictionary<string, Dictionary<string, string>> FriendSubs { get; set; } = new();

    [JsonPropertyName("deliveryReceipts")]
    public Dictionary<string, Dictionary<string, List<string>>> DeliveryReceipts { get; set; } = new();
}
