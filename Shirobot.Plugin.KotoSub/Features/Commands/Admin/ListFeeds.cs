using System.Globalization;
using System.Text;

namespace Shirobot.Plugin.KotoSub.Features.Commands.Admin;

internal sealed class ListFeeds(FeedRegistry feeds, SubscriptionRegistry subscriptions) : ISubCommand
{
    public IReadOnlyList<string> Names { get; } = ["feeds"];
    public string Usage => "feeds";
    public string Summary => "列出全部订阅源概况";
    public CommandAccess Access => CommandAccess.Owner;

    public async Task HandleAsync(CommandCall call)
    {
        var all = feeds.All();
        if (all.Count == 0)
        {
            await call.ReplyAsync("[订阅] 当前没有订阅源。");
            return;
        }

        var builder = new StringBuilder();
        builder.Append("[订阅] 全部订阅源 (").Append(all.Count).AppendLine(")");
        foreach (var feed in all.OrderBy(f => f.Id, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append("- ").Append(feed.Id).Append(" <- ").AppendLine(feed.Url);
            if (!string.IsNullOrWhiteSpace(feed.DisplayName))
                builder.Append("    title: ").AppendLine(feed.DisplayName);
            builder.Append("    interval=")
                .Append(feed.IntervalSeconds?.ToString(CultureInfo.InvariantCulture) ?? "default")
                .Append(", failures=").Append(feed.ConsecutiveFailures)
                .Append(", subs=").Append(subscriptions.SubscribersOf(feed.Id).Count)
                .AppendLine();
            if (feed.LastFetchAt.HasValue)
                builder.Append("    last_fetch=")
                    .AppendLine(feed.LastFetchAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
        }

        await call.ReplyAsync(builder.ToString().TrimEnd());
    }
}
