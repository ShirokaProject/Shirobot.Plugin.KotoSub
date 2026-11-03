namespace Shirobot.Plugin.KotoSub.Features.Cards.WordPress;

/// <summary>WordPress sites are recognised by the feed's generator tag, not by address.</summary>
internal sealed class WordPressCards : ICardStyle
{
    public string Id => "wordpress";
    public string Label => "WordPress";
    public IReadOnlyList<ICardRecipe> Recipes { get; } = [new CardRecipe<WordPressArticleCard>(CardCatalog.ArticleVariant)];
    public bool RendersFeedMetadata => true;
    public bool Matches(Uri uri) => false;
    public bool MatchesGenerator(string generator) => generator.Contains("wordpress", StringComparison.OrdinalIgnoreCase);
}
