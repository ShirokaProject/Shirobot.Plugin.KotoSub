using System.Net;
using Xunit;

namespace Shirobot.Plugin.KotoSub.Tests;

public sealed class PollingTests
{
    [Fact]
    public void FailureBackoffNeverPollsFasterThanNormalInterval()
    {
        var feed = new SubscriptionTarget { ConsecutiveFailures = 1 };
        var config = new KotoSubConfig
        {
            DefaultIntervalSeconds = 60,
            MinIntervalSeconds = 5,
            BackoffMaxSeconds = 10
        };

        Assert.Equal(TimeSpan.FromSeconds(60), BackoffPolicy.EffectiveInterval(feed, config));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestartMarksFeedBacklogSeenWithoutPushingIt(bool failFirstFetch)
    {
        var directory = Path.Combine(Path.GetTempPath(), "Shirobot-Rss-Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var state = new SubState
            {
                Targets = new Dictionary<string, PersistentTarget>
                {
                    ["news"] = new()
                    {
                        Key = "https://example.test/rss",
                        SeenUpdateIds = ["old-item"],
                        LastCheckedAt = DateTimeOffset.UtcNow.AddHours(-2)
                    }
                },
                GroupSubs = new Dictionary<string, Dictionary<string, string>>
                {
                    ["qq-official|group-openid"] = new() { ["news"] = "news" }
                }
            };
            var config = new KotoSubConfig
            {
                AllowPrivateUrls = true, MinIntervalSeconds = 1,
                DefaultIntervalSeconds = 1, BackoffMaxSeconds = 2
            };
            using var http = new HttpClient(new FeedHandler(failFirstFetch));
            new StateStore(directory).SaveSync(state);
            using var app = TestApp.Create(directory, state, config, http);
            var feeds = app.Targets;
            var scheduler = app.Poll;

            scheduler.Start();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(7));
            while (!feeds.TryGet("news", out var currentFeed) || !currentFeed.SeenUpdateIds.Contains("new-item"))
                await Task.Delay(25, timeout.Token);
            Assert.True(await scheduler.StopAsync());

            Assert.True(feeds.TryGet("news", out var feed));
            Assert.Contains("new-item", feed.SeenUpdateIds);
            Assert.Equal(0, feed.ConsecutiveFailures);
            var saved = new StateStore(directory).Load();
            Assert.Contains("new-item", saved.Targets["news"].SeenUpdateIds);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task RequestTimeoutCountsAsFailureAndSchedulesBackoff()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Shirobot-Rss-Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var state = new SubState
            {
                Targets = new Dictionary<string, PersistentTarget>
                {
                    ["slow"] = new() { Key = "https://example.test/slow" }
                }
            };
            var config = new KotoSubConfig
            {
                AllowPrivateUrls = true,
                RequestTimeoutSeconds = 5,
                MinIntervalSeconds = 30,
                DefaultIntervalSeconds = 30
            };
            using var http = new HttpClient(new SlowFeedHandler()) { Timeout = Timeout.InfiniteTimeSpan };
            new StateStore(directory).SaveSync(state);
            using var app = TestApp.Create(directory, state, config, http);
            var feeds = app.Targets;
            var scheduler = app.Poll;

            scheduler.Start();
            using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(9));
            while (!feeds.TryGet("slow", out var current) || current.ConsecutiveFailures == 0)
                await Task.Delay(25, wait.Token);
            Assert.True(await scheduler.StopAsync());

            Assert.True(feeds.TryGet("slow", out var feed));
            Assert.Equal(1, feed.ConsecutiveFailures);
            Assert.NotNull(feed.LastCheckedAt);
            Assert.Equal(1, new StateStore(directory).Load().Targets["slow"].ConsecutiveFailures);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void NewFeedIsPublishedWithItsBaselineAlreadySet()
    {
        var feeds = new TargetRegistry();
        var subscriptions = new SubscriptionRegistry();
        var subscriber = SubscriberKey.Group("qq", "123");
        var items = new[]
        {
            new UpdateEvent("newest", "Newest", "", "", null, [], null),
            new UpdateEvent("middle", "Middle", "", "", null, [], null),
            new UpdateEvent("oldest", "Oldest", "", "", null, [], null)
        };

        Assert.True(feeds.TryAddInitialized("news", "rss", "https://example.test/rss", "News", "owner",
            items, 2, out var feed, added => subscriptions.Add(subscriber, "news", added.Id)));
        Assert.Equal(["middle", "newest"], feed.SeenUpdateIds);
        Assert.NotNull(feed.LastCheckedAt);
        Assert.Equal("News", feed.DisplayName);
        Assert.True(subscriptions.TryGetFeed(subscriber, "news", out var subscribedFeed));
        Assert.Equal("news", subscribedFeed);
        Assert.False(feeds.TryAddInitialized("another", "rss", "https://example.test/rss", null, "owner",
            items, 2, out var reused));
        Assert.Same(feed, reused);
        Assert.Single(feeds.All());
    }

    [Fact]
    public void FeedUrlsKeepCaseSensitivePathsAndQueriesDistinct()
    {
        var feeds = new TargetRegistry();
        Assert.True(feeds.TryAddInitialized("upper", "rss", "https://EXAMPLE.test/Feed?type=A", null,
            "owner", [], 100, out _));

        Assert.NotNull(feeds.Find("rss", "https://example.TEST/Feed?type=A"));
        Assert.Null(feeds.Find("rss", "https://example.test/feed?type=A"));
        Assert.Null(feeds.Find("rss", "https://example.test/Feed?type=a"));
    }

    [Fact]
    public async Task SlowFeedDoesNotDelayOtherFeedChecks()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Shirobot-Rss-Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var state = new SubState
            {
                Targets = new Dictionary<string, PersistentTarget>
                {
                    ["slow"] = new() { Key = "https://example.test/slow" },
                    ["fast"] = new() { Key = "https://example.test/fast" }
                }
            };
            var config = new KotoSubConfig
            {
                AllowPrivateUrls = true, RequestTimeoutSeconds = 5,
                MinIntervalSeconds = 1, DefaultIntervalSeconds = 1
            };
            using var http = new HttpClient(new MixedSpeedFeedHandler()) { Timeout = Timeout.InfiniteTimeSpan };
            new StateStore(directory).SaveSync(state);
            using var app = TestApp.Create(directory, state, config, http);
            var feeds = app.Targets;
            var scheduler = app.Poll;

            scheduler.Start();
            using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            while (!feeds.TryGet("fast", out var fast) || !fast.SeenUpdateIds.Contains("second"))
                await Task.Delay(25, wait.Token);
            Assert.True(await scheduler.StopAsync());

            Assert.True(feeds.TryGet("fast", out var completed));
            Assert.Contains("second", completed.SeenUpdateIds);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task NotModifiedPollKeepsSeenItemsAndResetsFailures()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Shirobot-Rss-Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var state = new SubState
            {
                Targets = new Dictionary<string, PersistentTarget>
                {
                    ["news"] = new()
                    {
                        Key = "https://example.test/rss",
                        SeenUpdateIds = ["old"],
                        LastCheckedAt = DateTimeOffset.UtcNow.AddHours(-1),
                        ConsecutiveFailures = 2,
                        ETag = "\"v1\""
                    }
                }
            };
            var config = new KotoSubConfig
            {
                AllowPrivateUrls = true, MinIntervalSeconds = 1,
                DefaultIntervalSeconds = 1, BackoffMaxSeconds = 2
            };
            var handler = new NotModifiedHandler();
            using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            new StateStore(directory).SaveSync(state);
            using var app = TestApp.Create(directory, state, config, http);
            var feeds = app.Targets;
            var scheduler = app.Poll;
            app.Poll.Reset(new(), []);

            scheduler.Start();
            using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            while (!feeds.TryGet("news", out var current) || current.ConsecutiveFailures != 0)
                await Task.Delay(25, wait.Token);
            Assert.True(await scheduler.StopAsync());

            Assert.Equal("\"v1\"", handler.RequestedEtag);
            Assert.True(feeds.TryGet("news", out var feed));
            Assert.Equal(["old"], feed.SeenUpdateIds);
            Assert.Equal("\"v2\"", feed.ETag);
            Assert.Equal(0, new StateStore(directory).Load().Targets["news"].ConsecutiveFailures);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class NotModifiedHandler : HttpMessageHandler
    {
        public string? RequestedEtag { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedEtag = request.Headers.IfNoneMatch.SingleOrDefault()?.ToString();
            var response = new HttpResponseMessage(HttpStatusCode.NotModified);
            response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"v2\"");
            return Task.FromResult(response);
        }
    }

    private sealed class MixedSpeedFeedHandler : HttpMessageHandler
    {
        private int _fastRequests;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/slow")
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

            var second = Interlocked.Increment(ref _fastRequests) > 1
                ? "<item><guid>second</guid><title>Second</title></item>"
                : string.Empty;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"<rss><channel><item><guid>first</guid><title>First</title></item>{second}</channel></rss>")
            };
        }
    }

    private sealed class SlowFeedHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        }
    }

    private sealed class FeedHandler : HttpMessageHandler
    {
        private readonly bool _failFirst;
        private int _requests;

        public FeedHandler(bool failFirst) => _failFirst = failFirst;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_failFirst && Interlocked.Increment(ref _requests) == 1)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    <rss version="2.0"><channel><title>News</title>
                    <item><guid>old-item</guid><title>Old</title><link>https://example.test/old</link></item>
                    <item><guid>new-item</guid><title>Backlog</title><link>https://example.test/new</link></item>
                    </channel></rss>
                    """)
            });
        }
    }
}
