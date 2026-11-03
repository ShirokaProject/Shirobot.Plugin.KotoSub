using System.Globalization;

namespace Shirobot.Plugin.KotoSub.Features.Commands.Admin;

/// <summary>Bot owner sets how often a source is polled. Accepts this chat's alias or the key shown by "feeds".</summary>
internal sealed class SetInterval(
    SubscriptionRegistry subscriptions, FeedRegistry feeds, FeedProgress progress,
    Func<KotoSubConfig> config, StatePersister persister) : ISubCommand
{
    public IReadOnlyList<string> Names { get; } = ["interval"];
    public string Usage => "interval <别名> <sec>";
    public string Summary => "设置轮询间隔";
    public CommandAccess Access => CommandAccess.Owner;

    public async Task HandleAsync(CommandCall call)
    {
        if (call.Args.Count < 2)
        {
            await call.ReplyUsageAsync(this);
            return;
        }

        var name = FeedIdGenerator.Sanitize(call.Args[0]);
        var feedId = subscriptions.TryGetFeed(call.Scope, name, out var scoped) ? scoped : name;
        if (!feeds.TryGet(feedId, out _))
        {
            await call.ReplyAsync($"[订阅] 未找到 {name}。");
            return;
        }

        if (!int.TryParse(call.Args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) || seconds <= 0)
        {
            await call.ReplyAsync("[订阅] seconds 必须是正整数。");
            return;
        }

        seconds = Math.Max(seconds, config().MinIntervalSeconds);
        progress.SetInterval(feedId, seconds);
        persister.PersistSync();
        await call.ReplyAsync($"[订阅] {name} 间隔已设为 {seconds}s。");
    }
}
