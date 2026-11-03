using System.Text.Json.Serialization;

namespace Shirobot.Plugin.KotoSub.Infrastructure.Storage;


internal sealed class SubscriptionsDocument
{
    public const int CurrentVersion = 3;

    [JsonPropertyName("version")]
    public int Version { get; set; } = CurrentVersion;

    [JsonPropertyName("targets")]
    public Dictionary<string, PersistentTargetDefinition>? Targets { get; set; }

    // Legacy key written by versions before the generic subscription model.
    [JsonPropertyName("feeds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, PersistentTargetDefinition>? LegacyFeeds { get; set; }

    // subscriber → (alias → internal target id)
    [JsonPropertyName("groupAliases")]
    public Dictionary<string, Dictionary<string, string>>? GroupAliases { get; set; }

    [JsonPropertyName("friendAliases")]
    public Dictionary<string, Dictionary<string, string>>? FriendAliases { get; set; }

    // v1 (read-only): subscriber → target ids. Never written back.
    [JsonPropertyName("groupSubs")]
    public Dictionary<string, List<string>>? GroupSubs { get; set; }

    [JsonPropertyName("friendSubs")]
    public Dictionary<string, List<string>>? FriendSubs { get; set; }
}
