namespace Shirobot.Plugin.KotoSub.Features.Commands.Items;

internal sealed class TestPush(SubscriptionRegistry subscriptions, FeedRegistry feeds, ItemDelivery delivery)
    : ISubCommand
{
    public IReadOnlyList<string> Names { get; } = ["test"];
    public string Usage => "test <别名>";
    public string Summary => "立即拉最新一条到当前会话";
    public CommandAccess Access => CommandAccess.Manager;

    public async Task HandleAsync(CommandCall call)
    {
        if (call.Args.Count < 1)
        {
            await call.ReplyUsageAsync(this);
            return;
        }

        var alias = FeedIdGenerator.Sanitize(call.Args[0]);
        if (!subscriptions.TryGetFeed(call.Scope, alias, out var feedId) || !feeds.TryGet(feedId, out _))
        {
            await call.ReplyAsync($"[订阅] 当前会话未订阅 {alias}。");
            return;
        }

        var result = await delivery.FetchAndDeliverAsync(
            feedId, alias, "test", call.Scope,
            items => items.OrderByDescending(item => item.Published ?? DateTimeOffset.MinValue).Take(1).ToList(),
            call.Message.MessageId, includeImage: true, CancellationToken.None);

        if (!result.FeedFound)
            await call.ReplyAsync($"[订阅] 当前会话未订阅 {alias}。");
        else if (!result.FetchSucceeded || result.SelectedItems == 0)
            await call.ReplyAsync($"[订阅] {alias} 没有可用条目或抓取失败。");
    }
}
