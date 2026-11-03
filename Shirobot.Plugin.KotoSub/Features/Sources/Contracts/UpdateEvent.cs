namespace Shirobot.Plugin.KotoSub.Features.Sources.Contracts;

internal sealed record UpdateEvent(
    string Id,
    string Title,
    string Link,
    string Description,
    DateTimeOffset? Published,
    IReadOnlyList<string> Tags,
    string? FirstImageUrl)
{
    public string FullDescription { get; init; } = Description;
    public string? Kind { get; init; }
    public IReadOnlyList<UpdateAttachment> Attachments { get; init; } = [];
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    /// <summary>Ids to remember as "already seen", oldest first.</summary>
    public static IEnumerable<string> SeenOrder(IReadOnlyList<UpdateEvent> items) =>
        items.Reverse()
            .OrderBy(item => item.Published ?? DateTimeOffset.MinValue)
            .Select(item => item.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase);
}

internal sealed record UpdateAttachment(string Kind, string Url, string? Label = null);
