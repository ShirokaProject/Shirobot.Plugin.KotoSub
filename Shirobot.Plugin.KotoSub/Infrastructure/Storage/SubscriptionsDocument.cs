using System.Text.Json.Serialization;

namespace Shirobot.Plugin.KotoSub.Infrastructure.Storage;


internal sealed class SubscriptionsDocument
{
    public const int CurrentVersion = 2;

    [JsonPropertyName("version")]
    public int Version { get; set; } = CurrentVersion;

    [JsonPropertyName("feeds")]
    public Dictionary<string, PersistentFeedDefinition> Feeds { get; set; } = new();

    // v2: subscriber → (alias → internal feed id)
    [JsonPropertyName("groupAliases")]
    public Dictionary<string, Dictionary<string, string>>? GroupAliases { get; set; }

    [JsonPropertyName("friendAliases")]
    public Dictionary<string, Dictionary<string, string>>? FriendAliases { get; set; }

    // v1 (read-only): subscriber → feed ids. Never written back.
    [JsonPropertyName("groupSubs")]
    public Dictionary<string, List<string>>? GroupSubs { get; set; }

    [JsonPropertyName("friendSubs")]
    public Dictionary<string, List<string>>? FriendSubs { get; set; }
}
