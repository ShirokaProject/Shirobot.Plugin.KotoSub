namespace Shirobot.Plugin.KotoSub.Features.Sources.Rss;

/// <summary>RSS 2.0, Atom 1.0 and RDF/RSS 1.0 over HTTP(S).</summary>
internal sealed class RssSource(RssFetcher fetcher) : ISubscriptionSource
{
    public const string SourceId = "rss";

    public string Id => SourceId;
    public string DisplayName => "RSS / Atom";
    public bool Matches(Uri uri) => uri.Scheme is "http" or "https";

    /// <summary>"github.com/owner/repo" follows the repository's commits; any other address is used as typed.</summary>
    public AddressInput Normalize(Uri uri)
    {
        if (!FeedUrl.HostIs(uri, "github.com")) return new AddressInput(uri.AbsoluteUri);
        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || parts[1].EndsWith(".atom", StringComparison.OrdinalIgnoreCase))
            return new AddressInput(uri.AbsoluteUri);

        var commits = new UriBuilder(uri.Scheme, uri.Host)
        {
            Path = $"/{parts[0]}/{parts[1]}/commits.atom",
            Query = string.Empty,
            Fragment = string.Empty
        };
        return new AddressInput(commits.Uri.AbsoluteUri, FeedIdGenerator.Sanitize(parts[0]));
    }

    public Task<SourceUpdateBatch> FetchAsync(FetchRequest request, CancellationToken cancellationToken) =>
        fetcher.FetchAsync(request.TargetKey, request.ETag, request.LastModified, cancellationToken);
}
