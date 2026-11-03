namespace Shirobot.Plugin.KotoSub.Features.Cards;

/// <summary>Picks the style for an item and the template within that style. Unknown items get the generic card.</summary>
internal sealed class CardCatalog
{
    public const string ArticleVariant = "article";

    private readonly IReadOnlyList<ICardStyle> _styles;
    private readonly ICardStyle _generic;

    public CardCatalog(IEnumerable<ICardStyle> styles, ICardStyle generic)
    {
        _styles = styles.ToArray();
        _generic = generic;
        var duplicate = _styles.Append(generic).GroupBy(style => style.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new ArgumentException($"重复的卡片风格: {duplicate.Key}", nameof(styles));
        if (Find(generic, ArticleVariant) is null)
            throw new ArgumentException("通用风格缺少文章卡片。", nameof(generic));
    }

    /// <summary>Article link first (a feed mixing sites), then the feed address, then the generator tag.</summary>
    public ICardStyle StyleFor(SubscriptionTarget feed, UpdateEvent item)
    {
        if (Uri.TryCreate(item.Link, UriKind.Absolute, out var itemUri) &&
            _styles.FirstOrDefault(style => style.Matches(itemUri)) is { } byLink)
            return byLink;
        if (Uri.TryCreate(feed.Key, UriKind.Absolute, out var feedUri) &&
            _styles.FirstOrDefault(style => style.Matches(feedUri)) is { } byFeed)
            return byFeed;
        if (feed.Metadata.TryGetValue("generator", out var generator) &&
            _styles.FirstOrDefault(style => style.MatchesGenerator(generator)) is { } byGenerator)
            return byGenerator;
        return _generic;
    }

    public ICardRecipe RecipeFor(ICardStyle style, SubscriptionTarget feed, UpdateEvent item)
    {
        var variant = style.ResolveVariant(feed, item);
        return Find(style, variant) ?? Find(style, ArticleVariant) ?? Find(_generic, ArticleVariant)!;
    }

    private static ICardRecipe? Find(ICardStyle style, string variant) =>
        style.Recipes.FirstOrDefault(recipe => recipe.VariantId.Equals(variant, StringComparison.OrdinalIgnoreCase));
}
