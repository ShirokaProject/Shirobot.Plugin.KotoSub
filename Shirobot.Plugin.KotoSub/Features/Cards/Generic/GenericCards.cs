namespace Shirobot.Plugin.KotoSub.Features.Cards.Generic;

/// <summary>The plain article card used when no platform claims an item.</summary>
internal sealed class GenericCards : ICardStyle
{
    public string Id => "general";
    public string Label => "RSS";
    public IReadOnlyList<ICardRecipe> Recipes { get; } = [new CardRecipe<ArticleCard>(CardCatalog.ArticleVariant)];
    public bool ResolvesSiteAvatar => true;
    public bool Matches(Uri uri) => false;
}
