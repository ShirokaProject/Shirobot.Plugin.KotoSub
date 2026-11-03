namespace Shirobot.Plugin.KotoSub.Features.Sources.Contracts;

/// <summary>Conditional-request state remembered from the previous successful fetch.</summary>
internal sealed record FetchRequest(string Url, string? ETag = null, DateTimeOffset? LastModified = null);

/// <summary>An address ready to be fetched, with a short name to suggest for it.</summary>
internal sealed record AddressInput(string FeedUrl, string? SuggestedAlias = null);

/// <summary>
/// Where items come from. RSS / Atom is one source; another kind (an API, a page) only has to say which
/// addresses it can read and turn the remote state into a list of items.
/// </summary>
internal interface ISubscriptionSource
{
    string Id { get; }
    string DisplayName { get; }
    bool Matches(Uri uri);

    /// <summary>Turns a page address a person would paste into the address that is actually fetched.</summary>
    AddressInput Normalize(Uri uri);

    Task<FeedFetchResult> FetchAsync(FetchRequest request, CancellationToken cancellationToken);
}
