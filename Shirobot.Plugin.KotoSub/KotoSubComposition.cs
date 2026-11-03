using ShiroBot.SDK.Plugin;
using Shirobot.Plugin.KotoSub.Features.Cards;
using Shirobot.Plugin.KotoSub.Features.Commands;
using Shirobot.Plugin.KotoSub.Features.Commands.Admin;
using Shirobot.Plugin.KotoSub.Features.Commands.Items;
using Shirobot.Plugin.KotoSub.Features.Commands.Subscriptions;
using Shirobot.Plugin.KotoSub.Features.Delivery;
using Shirobot.Plugin.KotoSub.Features.Polling;
using Shirobot.Plugin.KotoSub.Features.Sources.Rss;
using Shirobot.Plugin.KotoSub.Features.Sources;
using Shirobot.Plugin.KotoSub.Infrastructure;

namespace Shirobot.Plugin.KotoSub;

/// <summary>Everything the plugin needs from the host, handed in once.</summary>
internal sealed record CompositionOptions(
    IBotContext Bot,
    Func<KotoSubConfig> Config,
    Action<KotoSubConfig> SaveConfig,
    Func<Task> Reload,
    HttpClient Http,
    HttpClient FeedHttp,
    StateStore Store,
    SubState State,
    IRenderContext? Render = null);

/// <summary>Wires every module together by hand; there is no DI container in a plugin.</summary>
[ArchitectureException("组合根：把所有模块装配到一起，依赖多是预期的")]
internal sealed class KotoSubComposition : IDisposable
{
    private readonly SourceFetchStep _fetchStep;
    private readonly BaselineTracker _baselines;

    private KotoSubComposition(
        TargetRegistry feeds, SubscriptionRegistry subscriptions, DeliveryReceiptStore receipts,
        BaselineTracker baselines, SourceFetchStep fetchStep, StatePersister persister,
        PollLoop poll, SubCommandRouter router, ItemDispatcher dispatcher)
    {
        Targets = feeds;
        Subscriptions = subscriptions;
        Receipts = receipts;
        _baselines = baselines;
        _fetchStep = fetchStep;
        Persister = persister;
        Poll = poll;
        Router = router;
        Dispatcher = dispatcher;
    }

    public TargetRegistry Targets { get; }
    public SubscriptionRegistry Subscriptions { get; }
    public DeliveryReceiptStore Receipts { get; }
    public StatePersister Persister { get; }
    public PollLoop Poll { get; }
    public SubCommandRouter Router { get; }
    public ItemDispatcher Dispatcher { get; }

    public static KotoSubComposition Create(CompositionOptions o)
    {
        var config = o.Config;
        var feeds = new TargetRegistry();
        var subscriptions = new SubscriptionRegistry();
        var receipts = new DeliveryReceiptStore();
        var progress = new TargetProgress(feeds);
        var baselines = new BaselineTracker(progress, receipts, config);
        Load(o.State, feeds, subscriptions, receipts, baselines);

        var sources = DefaultSources.Create(new RssFetcher(o.FeedHttp, config));
        var cards = new CardRenderer(o.Render, DefaultCardStyles.Create(o.FeedHttp), new CardMediaLoader(o.FeedHttp, config));
        var dispatcher = new ItemDispatcher(
            o.Bot, config, new QqOfficialDelivery(o.Bot, config, cards),
            new ItemText(new ImageEmbedder(o.Http), cards, config));

        var persister = new StatePersister(o.Store, () =>
        {
            var subs = subscriptions.Snapshot();
            return new SubState
            {
                Targets = feeds.Snapshot(),
                GroupSubs = subs.Groups,
                FriendSubs = subs.Friends,
                DeliveryReceipts = receipts.Snapshot(feedId => feeds.TryGet(feedId, out _))
            };
        });

        var fetchStep = new SourceFetchStep(progress, sources, persister, config);
        var runner = new UpdateCheckRunner(
            fetchStep, new ItemPusher(subscriptions, dispatcher, receipts, progress, config), baselines, progress, persister);
        var poll = new PollLoop(feeds, baselines, receipts, runner, config);

        var onDemand = new OnDemandFetch(feeds, progress, sources);
        var delivery = new ItemDelivery(onDemand, feeds, dispatcher);
        var formatter = new SubscriptionFormatter(feeds);
        List<ISubCommand> commands = [];
        commands.Add(new ShowHelp(commands));
        commands.AddRange(
        [
            new ShowStatus(subscriptions, formatter, config),
            new ListSubscriptions(subscriptions, formatter),
            new AddSubscription(sources, onDemand, new TargetEnrollment(feeds, config), subscriptions, persister),
            new RemoveSubscription(subscriptions, feeds, persister),
            new RenameAlias(subscriptions, persister),
            new ShowLatest(subscriptions, feeds, delivery, config),
            new TestPush(subscriptions, feeds, delivery),
            new ManageConfig(config, o.SaveConfig),
            new SetInterval(subscriptions, feeds, progress, config, persister),
            new ListTargets(feeds, subscriptions),
            new ReloadState(o.Reload)
        ]);

        return new KotoSubComposition(feeds, subscriptions, receipts, baselines, fetchStep, persister, poll,
            new SubCommandRouter(o.Bot, commands, subscriptions, persister), dispatcher);
    }

    /// <summary>Replaces the in-memory state with <paramref name="state"/>; the poll loop must be stopped.</summary>
    public void Reload(SubState state) => Load(state, Targets, Subscriptions, Receipts, _baselines);

    public void Dispose()
    {
        Poll.Dispose();
        _fetchStep.Dispose();
        Persister.Dispose();
    }

    private static void Load(
        SubState state, TargetRegistry feeds, SubscriptionRegistry subscriptions,
        DeliveryReceiptStore receipts, BaselineTracker baselines)
    {
        feeds.LoadFrom(state.Targets);
        subscriptions.LoadFrom(state.GroupSubs, state.FriendSubs);
        receipts.Load(state.DeliveryReceipts);
        baselines.Reset(state.Targets.Keys);
    }
}
