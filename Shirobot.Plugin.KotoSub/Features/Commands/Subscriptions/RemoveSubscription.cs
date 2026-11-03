namespace Shirobot.Plugin.KotoSub.Features.Commands.Subscriptions;

internal sealed class RemoveSubscription(
    SubscriptionRegistry subscriptions, TargetRegistry feeds, StatePersister persister) : ISubCommand
{
    public IReadOnlyList<string> Names { get; } = ["remove", "rm", "del"];
    public string Usage => "remove <别名>";
    public string Summary => "退订";
    public CommandAccess Access => CommandAccess.Manager;

    public async Task HandleAsync(CommandCall call)
    {
        if (call.Args.Count < 1)
        {
            await call.ReplyUsageAsync(this);
            return;
        }

        var alias = FeedIdGenerator.Sanitize(call.Args[0]);
        var feedId = subscriptions.Remove(call.Scope, alias);
        if (feedId is null)
        {
            await call.ReplyMentionAsync($"[订阅] 当前会话未订阅 {alias}。");
            return;
        }

        if (subscriptions.SubscribersOf(feedId).Count == 0)
        {
            feeds.Remove(feedId);
            BotLog.Info($"[KotoSub] feed={feedId} 已无订阅者，自动清理。");
        }

        persister.PersistSync();
        await call.ReplyMentionAsync($"[订阅] 已退订 {alias}。");
    }
}
