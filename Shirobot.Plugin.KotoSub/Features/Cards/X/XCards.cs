namespace Shirobot.Plugin.KotoSub.Features.Cards.X;

internal sealed class XCards : ICardStyle
{
    private const string PostVariant = "post";

    public string Id => "x";
    public string Label => "X / Twitter";
    public IReadOnlyList<ICardRecipe> Recipes { get; } = [new CardRecipe<PostCard>(PostVariant)];

    public bool Matches(Uri uri) =>
        FeedUrl.HostIs(uri, "x.com") || FeedUrl.HostIs(uri, "twitter.com") || IsRssHubTwitterUserRoute(uri);

    public string ResolveVariant(SubscriptionTarget feed, UpdateEvent item) => PostVariant;

    private static bool IsRssHubTwitterUserRoute(Uri uri)
    {
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2 &&
               segments[0].Equals("twitter", StringComparison.OrdinalIgnoreCase) &&
               segments[1].Equals("user", StringComparison.OrdinalIgnoreCase);
    }
}
