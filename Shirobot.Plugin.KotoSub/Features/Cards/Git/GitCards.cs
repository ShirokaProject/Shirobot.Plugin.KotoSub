namespace Shirobot.Plugin.KotoSub.Features.Cards.Git;

/// <summary>One style for any Git host (GitHub, Gitea): commits, pull requests and issues share a card.</summary>
internal sealed class GitCards(string id, string label, Func<Uri, bool> matches) : ICardStyle
{
    public string Id { get; } = id;
    public string Label { get; } = label;

    public IReadOnlyList<ICardRecipe> Recipes { get; } =
    [
        new CardRecipe<GitEventCard>("commit"),
        new CardRecipe<GitEventCard>("pull_request"),
        new CardRecipe<GitEventCard>("issue"),
        new CardRecipe<GitEventCard>("activity")
    ];

    public bool Matches(Uri uri) => matches(uri);

    public string ResolveVariant(SubscriptionTarget feed, UpdateEvent item) => GitCardInfo.From(item).Variant;

    public Task<CardDataSupplement?> LoadDataAsync(UpdateEvent item, string variant, CancellationToken cancellationToken)
    {
        var info = GitCardInfo.From(item);
        return Task.FromResult<CardDataSupplement?>(new CardDataSupplement(
            GitEventTypeText: info.EventType, GitStatusText: info.Status,
            GitRepositoryText: info.Repository, GitReferenceText: info.Reference));
    }
}
