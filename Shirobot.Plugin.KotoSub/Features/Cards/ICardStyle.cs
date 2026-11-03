namespace Shirobot.Plugin.KotoSub.Features.Cards;

/// <summary>
/// How cards look for one platform (Bilibili, X, GitHub ...): which items it recognises, which templates it has,
/// which template suits an item, and any extra data worth showing. Everything platform-specific lives in
/// one style class; the card engine itself knows no platform.
/// </summary>
internal interface ICardStyle
{
    string Id { get; }

    /// <summary>Name printed on the card and in captions.</summary>
    string Label { get; }

    IReadOnlyList<ICardRecipe> Recipes { get; }

    bool Matches(Uri uri);

    /// <summary>Platforms recognised by the feed's generator tag rather than by address (WordPress).</summary>
    bool MatchesGenerator(string generator) => false;

    /// <summary>True when the template already prints the feed's name and icon, so captions can skip them.</summary>
    bool RendersFeedMetadata => false;

    /// <summary>True when the site's own icon should stand in for a missing author avatar.</summary>
    bool ResolvesSiteAvatar => false;

    string ResolveVariant(SubscriptionTarget feed, UpdateEvent item) => CardCatalog.ArticleVariant;

    Task<CardDataSupplement?> LoadDataAsync(UpdateEvent item, string variant, CancellationToken cancellationToken) =>
        Task.FromResult<CardDataSupplement?>(null);
}
