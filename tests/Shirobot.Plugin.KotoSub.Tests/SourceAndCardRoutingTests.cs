using ShiroBot.SDK.Models;
using Xunit;

namespace Shirobot.Plugin.KotoSub.Tests;

public sealed class SourceAndCardRoutingTests
{
    private static readonly SourceCatalog Sources =
        DefaultSources.Create(new RssFetcher(new HttpClient(), () => new KotoSubConfig()));
    private static readonly CardCatalog Cards = DefaultCardStyles.Create(new HttpClient());

    [Theory]
    [InlineData("https://github.com/owner/repo", "https://github.com/owner/repo/commits.atom", "owner")]
    [InlineData("https://github.com/owner/repo/commits.atom", "https://github.com/owner/repo/commits.atom", null)]
    [InlineData("https://www.bilibili.com/read/cv123", "https://www.bilibili.com/read/cv123", null)]
    [InlineData("https://blog.example.org/feed.xml", "https://blog.example.org/feed.xml", null)]
    [InlineData("https://github.com.evil.example/owner/repo", "https://github.com.evil.example/owner/repo", null)]
    public void RssSourceRewritesOnlyRealGitHubRepositoryAddresses(string typed, string fetched, string? alias)
    {
        var source = Sources.ForAddress(typed);
        var address = source.Normalize(new Uri(typed));

        Assert.Equal("rss", source.Id);
        Assert.Equal(fetched, address.FeedUrl);
        Assert.Equal(alias, address.SuggestedAlias);
    }

    [Theory]
    [InlineData("ftp://example.org/feed")]
    [InlineData("not a url")]
    public void OnlyHttpAddressesAreAccepted(string typed) =>
        Assert.Throws<ArgumentException>(() => Sources.ForAddress(typed));

    [Theory]
    [InlineData("https://github.com/owner/repo/commit/abcdef123456", "github")]
    [InlineData("https://git.example.org/owner/repo/issues/3", "gitea")]
    [InlineData("https://www.bilibili.com/read/cv123", "bilibili")]
    [InlineData("https://x.com/user/status/123", "x")]
    [InlineData("https://blog.example.org/post", "general")]
    [InlineData("https://github.com.evil.example/owner/repo", "general")]
    public void CardStyleFollowsTheExactDomainOfTheArticle(string link, string styleId)
    {
        var feed = new FeedSource { Id = "f", Url = "https://rsshub.example/feed" };
        Assert.Equal(styleId, Cards.StyleFor(feed, Item(link)).Id);
    }

    [Fact]
    public void WordPressIsRecognisedByGeneratorOnlyWhenNoSiteMatches()
    {
        var wordpress = new FeedSource { Id = "wp", Url = "https://blog.example.org/feed", Generator = "https://wordpress.org/?v=6.4" };
        Assert.Equal("wordpress", Cards.StyleFor(wordpress, Item("https://blog.example.org/post")).Id);
        Assert.Equal("github", Cards.StyleFor(wordpress, Item("https://github.com/o/r/issues/1")).Id);
    }

    [Theory]
    [InlineData("https://www.bilibili.com/video/BV123", "video")]
    [InlineData("https://www.bilibili.com/opus/123", "dynamic")]
    [InlineData("https://www.bilibili.com/read/cv123", "article")]
    public void BilibiliUsesArticleLinkToChooseCard(string link, string variant)
    {
        var feed = new FeedSource { Id = "bili", Url = "https://rsshub.example/bilibili/user/dynamic/1" };
        var item = Item(link);
        var recipe = Cards.RecipeFor(Cards.StyleFor(feed, item), feed, item);
        Assert.Equal(variant, recipe.VariantId);
    }

    [Fact]
    public void EveryStyleFallsBackToAnArticleCard()
    {
        var feed = new FeedSource { Id = "x", Url = "https://rsshub.example/feed" };
        var item = Item("https://example.org/post");
        Assert.Equal("article", Cards.RecipeFor(Cards.StyleFor(feed, item), feed, item).VariantId);
    }

    [Fact]
    public void SourceKindSurvivesFeedRegistrySnapshot()
    {
        var registry = new FeedRegistry();
        Assert.True(registry.TryAddInitialized("video", "https://example.org/feed", "Videos", null,
            [], 100, out _, sourceKind: "rss"));
        var restored = new FeedRegistry();
        restored.LoadFrom(registry.Snapshot());
        Assert.True(restored.TryGet("video", out var feed));
        Assert.Equal("rss", feed.SourceKind);
    }

    [Fact]
    public async Task MissingRendererFallsBackToTextWithArticleLink()
    {
        using var http = new HttpClient();
        var text = new ItemText(new ImageEmbedder(http), null, () => new KotoSubConfig { EnableRenderedCards = true });
        var segments = await text.BuildSegmentsAsync(
            new FeedSource { Id = "blog" }, Item("https://example.org/post"), false, CancellationToken.None);
        var body = Assert.Single(segments.OfType<TextSegment>()).Text;
        Assert.Contains("title", body);
        Assert.Contains("https://example.org/post", body);
    }

    private static FeedItem Item(string link) => new("1", "title", link, "", null, [], null);
}
