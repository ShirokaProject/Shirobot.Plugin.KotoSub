namespace Shirobot.Plugin.KotoSub.Features.Sources.Contracts;

internal sealed class SubscriptionTarget
{
    public string Id { get; set; } = string.Empty;
    /// <summary>Provider-defined identity, such as an RSS URL or a platform-specific account ID.</summary>
    public string Key { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? SourceImageUrl { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int? IntervalSeconds { get; set; }
    public List<string> SeenUpdateIds { get; set; } = new();
    public DateTimeOffset? LastCheckedAt { get; set; }
    public string? ETag { get; set; }
    public DateTimeOffset? LastModified { get; set; }
    public int ConsecutiveFailures { get; set; }
    /// <summary>Opaque provider-owned cursor/checkpoint data.</summary>
    public Dictionary<string, string> ProviderState { get; set; } = new(StringComparer.Ordinal);
    public string? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string DisplayOrHost => !string.IsNullOrWhiteSpace(DisplayName)
        ? DisplayName!
        : Uri.TryCreate(Key, UriKind.Absolute, out var uri) ? uri.IdnHost : Key;
}
