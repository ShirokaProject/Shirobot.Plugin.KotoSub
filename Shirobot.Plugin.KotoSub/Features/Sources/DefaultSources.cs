using Shirobot.Plugin.KotoSub.Features.Sources.Rss;

namespace Shirobot.Plugin.KotoSub.Features.Sources;

internal static class DefaultSources
{
    public static SourceCatalog Create(RssFetcher fetcher) => new([new RssSource(fetcher)]);
}
