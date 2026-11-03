namespace Shirobot.Plugin.KotoSub.Features.Sources.Contracts;

/// <summary>Picks the source that can read an address, or the one a feed was created with.</summary>
internal sealed class SourceCatalog
{
    private readonly IReadOnlyList<ISubscriptionSource> _sources;

    public SourceCatalog(IEnumerable<ISubscriptionSource> sources)
    {
        _sources = sources.ToArray();
        if (_sources.Count == 0)
            throw new ArgumentException("至少需要一个订阅源。", nameof(sources));
        var duplicate = _sources.GroupBy(source => source.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new ArgumentException($"重复的订阅源 ID: {duplicate.Key}", nameof(sources));
    }

    public ISubscriptionSource ForAddress(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new ArgumentException("订阅地址必须是 HTTP(S) URL。", nameof(url));
        return _sources.FirstOrDefault(source => source.Matches(uri))
               ?? throw new ArgumentException("没有可以读取该地址的订阅源。", nameof(url));
    }

    public ISubscriptionSource ForFeed(FeedSource feed) =>
        _sources.FirstOrDefault(source => source.Id.Equals(feed.SourceKind, StringComparison.OrdinalIgnoreCase))
        ?? ForAddress(feed.Url);
}
