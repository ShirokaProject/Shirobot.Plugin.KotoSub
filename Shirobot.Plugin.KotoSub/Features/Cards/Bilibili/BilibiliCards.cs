namespace Shirobot.Plugin.KotoSub.Features.Cards.Bilibili;

internal sealed class BilibiliCards(BilibiliVideoDetails videos) : ICardStyle
{
    public string Id => "bilibili";
    public string Label => "Bilibili";

    public IReadOnlyList<ICardRecipe> Recipes { get; } =
    [
        new CardRecipe<ArticleCard>(CardCatalog.ArticleVariant),
        new CardRecipe<VideoCard>("video"),
        new CardRecipe<DynamicCard>("dynamic")
    ];

    public bool Matches(Uri uri) => FeedUrl.HostIs(uri, "bilibili.com") || FeedUrl.HostIs(uri, "b23.tv");

    public string ResolveVariant(SubscriptionTarget feed, UpdateEvent item)
    {
        if (HasPath(item.Link, "/video/")) return "video";
        if (HasPath(item.Link, "/opus/") || HasPath(item.Link, "/dynamic/") ||
            Uri.TryCreate(item.Link, UriKind.Absolute, out var itemUri) &&
            FeedUrl.HostIs(itemUri, "t.bilibili.com")) return "dynamic";
        if (HasPath(item.Link, "/read/")) return CardCatalog.ArticleVariant;
        if (HasPath(feed.Key, "/bilibili/user/video/")) return "video";
        if (HasPath(feed.Key, "/bilibili/user/dynamic/")) return "dynamic";
        return CardCatalog.ArticleVariant;
    }

    public Task<CardDataSupplement?> LoadDataAsync(UpdateEvent item, string variant, CancellationToken cancellationToken) =>
        variant == "video" ? videos.TryLoadAsync(item, cancellationToken) : Task.FromResult<CardDataSupplement?>(null);

    private static bool HasPath(string url, string part) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.AbsolutePath.Contains(part, StringComparison.OrdinalIgnoreCase);
}
