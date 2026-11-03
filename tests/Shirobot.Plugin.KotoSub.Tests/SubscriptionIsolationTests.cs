using System.Net;
using System.Reflection;
using ShiroBot.SDK.Models;
using ShiroBot.SDK.Plugin;
using Xunit;

namespace Shirobot.Plugin.KotoSub.Tests;

public sealed class SubscriptionIsolationTests : IDisposable
{
    private const string FeedA = "https://feeds.example.test/alpha?token=SECRET-A";
    private const string FeedB = "https://feeds.example.test/beta";

    private readonly Harness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task TwoGroupsMayUseTheSameAliasForDifferentFeeds()
    {
        Assert.Contains("已订阅 news", await _h.Say($"#rss add {FeedA} news", target: "group-1"));
        Assert.Contains("已订阅 news", await _h.Say($"#rss add {FeedB} news", target: "group-2"));

        Assert.Contains("alpha", await _h.Say("#rss list", target: "group-1"));
        Assert.DoesNotContain("beta", await _h.Say("#rss list", target: "group-1"));
        Assert.Contains("beta", await _h.Say("#rss list", target: "group-2"));
        Assert.DoesNotContain("alpha", await _h.Say("#rss list", target: "group-2"));
    }

    [Fact]
    public async Task SubscribingToAnAlreadyKnownUrlRepliesTheSameAsTheFirstSubscriber()
    {
        var first = await _h.Say($"#rss add {FeedB} daily", target: "group-1");
        var second = await _h.Say($"#rss add {FeedB} daily", target: "group-2");

        Assert.Equal(first, second);
        Assert.DoesNotContain("已存在", second);
        Assert.DoesNotContain("已被", second);
    }

    [Fact]
    public async Task AnotherGroupsAliasOrInternalKeyCannotBeUsedToSubscribe()
    {
        await _h.Say($"#rss add {FeedA} secret-news", target: "group-1");
        var internalKey = Assert.Single(_h.Feeds.All()).Id;

        foreach (var guess in new[] { "secret-news", internalKey, "alpha" })
        {
            var reply = await _h.Say($"#rss add {guess}", target: "group-2");
            Assert.Contains("用法", reply);
        }

        Assert.Contains("没有订阅", await _h.Say("#rss list", target: "group-2"));
        Assert.Single(_h.Subscriptions.SubscribersOf(internalKey));
    }

    [Theory]
    [InlineData("remove {0}")]
    [InlineData("latest {0}")]
    [InlineData("test {0}")]
    [InlineData("rename {0} other")]
    public async Task ProbingForAnotherGroupsAliasLooksExactlyLikeProbingForNothing(string template)
    {
        await _h.Say($"#rss add {FeedA} secret-news", target: "group-1");

        var probeReal = await _h.Say("#rss " + string.Format(template, "secret-news"), target: "group-2");
        var probeNothing = await _h.Say("#rss " + string.Format(template, "never-used"), target: "group-2");

        Assert.Equal(probeNothing.Replace("never-used", "X"), probeReal.Replace("secret-news", "X"));
        Assert.Single(_h.Subscriptions.List(Key("group-1")));
    }

    [Fact]
    public async Task AnotherGroupCannotRemoveOrRenameMySubscription()
    {
        await _h.Say($"#rss add {FeedA} mine", target: "group-1");

        await _h.Say("#rss remove mine", target: "group-2");
        await _h.Say("#rss rename mine stolen", target: "group-2");

        var kept = Assert.Single(_h.Subscriptions.List(Key("group-1")));
        Assert.Equal("mine", kept.Alias);
    }

    [Fact]
    public async Task SameGroupIdOnDifferentAdapterInstancesIsolated()
    {
        await _h.Say($"#rss add {FeedA} mine", instance: "inst-a", target: "123456");

        Assert.Contains("没有订阅", await _h.Say("#rss list", instance: "inst-b", target: "123456"));
        Assert.Contains("未订阅", await _h.Say("#rss latest mine", instance: "inst-b", target: "123456"));
        Assert.Contains("未订阅", await _h.Say("#rss remove mine", instance: "inst-b", target: "123456"));
        Assert.Single(_h.Subscriptions.List(Key("123456", instance: "inst-a")));
    }

    [Fact]
    public async Task PrivateChatAndGroupWithTheSameIdAreSeparate()
    {
        await _h.Say($"#rss add {FeedA} mine", target: "42", group: false, sender: "42");

        Assert.Contains("没有订阅", await _h.Say("#rss list", target: "42"));
        Assert.Contains("mine", await _h.Say("#rss list", target: "42", group: false, sender: "42"));
    }

    [Fact]
    public async Task PlainGroupMembersSeeOnlyTheHostNeverThePrivateUrl()
    {
        await _h.Say($"#rss add {FeedA} mine", target: "group-1", role: MemberRole.Admin);

        var member = await _h.Say("#rss list", target: "group-1", sender: "plain", role: MemberRole.Member);
        var memberStatus = await _h.Say("#rss", target: "group-1", sender: "plain", role: MemberRole.Member);
        var admin = await _h.Say("#rss list", target: "group-1", role: MemberRole.Admin);

        Assert.Contains("feeds.example.test", member);
        Assert.DoesNotContain("SECRET-A", member);
        Assert.DoesNotContain("SECRET-A", memberStatus);
        Assert.Contains("SECRET-A", admin);
    }

    [Fact]
    public async Task ConfigAndRuntimeDetailsAreOwnerOnly()
    {
        var config = await _h.Say("#rss config", target: "group-1", sender: "plain", role: MemberRole.Admin);
        var status = await _h.Say("#rss", target: "group-1", sender: "plain", role: MemberRole.Admin);

        Assert.DoesNotContain("user_agent", config);
        Assert.Contains("仅 Bot 主人", config);
        Assert.DoesNotContain("allow_private_urls", status);
        Assert.Contains("user_agent", await _h.Say("#rss config", sender: Harness.Owner));
    }

    [Fact]
    public async Task OwnerFeedListingRevealsCountsButNeverWhichChatsSubscribe()
    {
        await _h.Say($"#rss add {FeedB} daily", target: "group-777");
        await _h.Say($"#rss add {FeedB} daily", target: "group-888");

        var listing = await _h.Say("#rss feeds", sender: Harness.Owner);

        Assert.Contains("subs=2", listing);
        Assert.DoesNotContain("group-777", listing);
        Assert.DoesNotContain("group-888", listing);
    }

    [Fact]
    public async Task FeedIsRemovedOnlyAfterTheLastSubscriberLeaves()
    {
        await _h.Say($"#rss add {FeedB} daily", target: "group-1");
        await _h.Say($"#rss add {FeedB} other-name", target: "group-2");

        await _h.Say("#rss remove daily", target: "group-1");
        Assert.Single(_h.Feeds.All());
        Assert.Single(_h.Subscriptions.List(Key("group-2")));

        await _h.Say("#rss remove other-name", target: "group-2");
        Assert.Empty(_h.Feeds.All());
    }

    [Fact]
    public async Task LegacySubscriptionsAreAdoptedOnlyWhenOneInstanceServesThePlatform()
    {
        _h.Subscriptions.LoadFrom(
            new Dictionary<string, Dictionary<string, string>> { ["qq|g"] = new() { ["old"] = "old-feed" } },
            new Dictionary<string, Dictionary<string, string>>());

        // Two "qq" instances are loaded: ownership of the legacy entry is ambiguous.
        Assert.Contains("没有订阅", await _h.Say("#rss list", instance: "inst-a", target: "g"));
        Assert.Contains("没有订阅", await _h.Say("#rss list", instance: "inst-b", target: "g"));

        _h.Instances.RemoveAll(instance => instance.Id == "inst-b");
        Assert.Contains("old", await _h.Say("#rss list", instance: "inst-a", target: "g"));
    }

    [Fact]
    public async Task DeliveryTargetsOfOneFeedAreKnownOnlyToTheScheduler()
    {
        await _h.Say($"#rss add {FeedB} daily", target: "group-1", instance: "inst-a");
        await _h.Say($"#rss add {FeedB} news", target: "group-1", instance: "inst-b");
        var feedId = Assert.Single(_h.Feeds.All()).Id;

        var subscribers = _h.Subscriptions.SubscribersOf(feedId);

        Assert.Equal(2, subscribers.Count);
        Assert.Contains(subscribers, s => s.Key.InstanceId == "inst-a" && s.Alias == "daily");
        Assert.Contains(subscribers, s => s.Key.InstanceId == "inst-b" && s.Alias == "news");
    }

    private static SubscriberKey Key(string group, string instance = "inst-a") =>
        SubscriberKey.Group("qq", group, instance);

    internal sealed class Harness : IDisposable
    {
        public const string Owner = "owner-1";

        private readonly string _directory = Path.Combine(
            Path.GetTempPath(), "Shirobot-Rss-Isolation", Guid.NewGuid().ToString("N"));
        private readonly HttpClient _http;
        private readonly KotoSubComposition _app;
        private readonly MessageProxy _messages;

        public Harness()
        {
            Directory.CreateDirectory(_directory);
            Instances =
            [
                new AdapterInstanceInfo("inst-a", "A", "pkg", "Adapter", "1", "qq", null),
                new AdapterInstanceInfo("inst-b", "B", "pkg", "Adapter", "1", "qq", null)
            ];
            var config = new KotoSubConfig { AllowPrivateUrls = true };
            _http = new HttpClient(new AnyFeedHandler());
            var state = new SubState();
            var store = new StateStore(_directory);
            store.SaveSync(state);

            var message = DispatchProxy.Create<IMessageContext, MessageProxy>();
            _messages = (MessageProxy)(object)message;
            var bot = DispatchProxy.Create<IBotContext, BotProxy>();
            var botProxy = (BotProxy)(object)bot;
            botProxy.Message = message;
            botProxy.Instances = Instances;
            botProxy.Owners = [Owner];

            _app = TestApp.Create(_directory, state, config, _http, bot);
        }

        public FeedRegistry Feeds => _app.Feeds;
        public SubscriptionRegistry Subscriptions => _app.Subscriptions;
        public List<AdapterInstanceInfo> Instances { get; }

        public async Task<string> Say(
            string text, string instance = "inst-a", string target = "group-1", string sender = "admin-1",
            bool group = true, MemberRole role = MemberRole.Admin)
        {
            var message = new MessageEvent
            {
                Platform = "qq",
                InstanceId = instance,
                MessageId = "msg-1",
                Channel = group ? Channel.Group(target) : Channel.Direct(target),
                Sender = new User(sender),
                Member = group ? new Member(new User(sender)) { Role = role } : null,
                Segments = [new TextSegment(text)]
            };

            _messages.Replies.Clear();
            await (group ? _app.Router.HandleGroupAsync(message) : _app.Router.HandleDirectAsync(message));
            return string.Join("\n", _messages.Replies);
        }

        public void Dispose()
        {
            _app.Dispose();
            _http.Dispose();
            try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
        }
    }

    public class BotProxy : DispatchProxy
    {
        public IMessageContext? Message { get; set; }
        public IReadOnlyList<AdapterInstanceInfo> Instances { get; set; } = [];
        public HashSet<string> Owners { get; set; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod!.Name switch
        {
            "get_Message" => Message,
            "get_Platform" => "qq",
            "GetAdapterInstances" => Instances,
            "IsAdmin" or "IsOwner" => args![0] is string id && Owners.Contains(id),
            _ => targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null
        };
    }

    public class MessageProxy : DispatchProxy
    {
        public List<string> Replies { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.Name != "QuoteReplyAsync")
                return targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;

            var text = string.Concat(args!.SelectMany(arg => arg switch
            {
                string value => [value],
                IEnumerable<MessageSegment> segments => segments.OfType<TextSegment>().Select(segment => segment.Text),
                _ => []
            }));
            Replies.Add(text);
            return Task.FromResult(new SentMessage("reply-1"));
        }
    }

    private sealed class AnyFeedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"""
                    <rss version="2.0"><channel><title>Feed {request.RequestUri!.AbsolutePath.Trim('/')}</title>
                    <item><guid>g1</guid><title>T</title><link>https://example.test/1</link></item>
                    </channel></rss>
                    """)
            });
    }
}
