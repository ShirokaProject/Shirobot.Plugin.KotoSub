namespace Shirobot.Plugin.KotoSub.Features.Sources.Contracts;

internal sealed record SourceUpdateBatch(
    string? SourceName,
    IReadOnlyList<UpdateEvent> Items)
{
    public string? SourceImageUrl { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, string> ProviderState { get; init; } = new Dictionary<string, string>();
    public bool NotModified { get; init; }
    public string? ETag { get; init; }
    public DateTimeOffset? LastModified { get; init; }
}
