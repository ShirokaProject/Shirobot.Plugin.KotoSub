namespace Shirobot.Plugin.KotoSub.Features.Sources.Contracts;

internal sealed record FeedItem(
    string Id,
    string Title,
    string Link,
    string Description,
    DateTimeOffset? Published,
    IReadOnlyList<string> Tags,
    string? FirstImageUrl)
{
    public string FullDescription { get; init; } = Description;

    /// <summary>Ids to remember as "already seen", oldest first.</summary>
    public static IEnumerable<string> SeenOrder(IReadOnlyList<FeedItem> items) =>
        items.Reverse()
            .OrderBy(item => item.Published ?? DateTimeOffset.MinValue)
            .Select(item => item.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase);
}
