namespace Shirobot.Plugin.KotoSub.Features.Sources.Contracts;

/// <summary>Conditional-request state remembered from the previous successful fetch.</summary>
internal sealed record FetchRequest(
    string TargetKey,
    string? ETag = null,
    DateTimeOffset? LastModified = null,
    IReadOnlyDictionary<string, string>? ProviderState = null);

/// <summary>An address ready to be fetched, with a short name to suggest for it.</summary>
internal sealed record AddressInput(string TargetKey, string? SuggestedAlias = null);

/// <summary>
/// Where subscription updates come from. A provider can map a user-facing address to a stable target key,
/// then turn provider data into generic update events.
/// </summary>
internal interface ISubscriptionSource
{
    string Id { get; }
    string DisplayName { get; }
    bool Matches(Uri uri);

    /// <summary>Turns a page address a person would paste into the address that is actually fetched.</summary>
    AddressInput Normalize(Uri uri);

    Task<SourceUpdateBatch> FetchAsync(FetchRequest request, CancellationToken cancellationToken);
}
