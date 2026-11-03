using System.Globalization;

namespace Shirobot.Plugin.KotoSub.Features.Commands.Items;

internal sealed class ShowLatest(
    SubscriptionRegistry subscriptions, FeedRegistry feeds, ItemDelivery delivery, Func<KotoSubConfig> config)
    : ISubCommand
{
    public IReadOnlyList<string> Names { get; } = ["latest"];
    public string Usage => "latest <别名> [tag] [n=N]";
    public string Summary => "立即拉最新（仅本会话已订阅）";
    public CommandAccess Access => CommandAccess.Anyone;

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

        string? tag = null;
        var count = 1;
        foreach (var token in call.Args.Skip(1))
        {
            if (token.StartsWith("n=", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(token[2..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                count = Math.Clamp(parsed, 1, Math.Max(1, config().LatestMaxN));
            else if (string.IsNullOrEmpty(tag))
                tag = token;
        }

        var result = await delivery.FetchAndDeliverAsync(
            feedId, alias, "latest", call.Scope,
            items => ByTag(items, tag)
                .OrderByDescending(item => item.Published ?? DateTimeOffset.MinValue)
                .Take(count).ToList(),
            call.Message.MessageId, includeImage: true, CancellationToken.None);

        if (!result.FeedFound)
            await call.ReplyAsync($"[订阅] 当前会话未订阅 {alias}。");
        else if (!result.FetchSucceeded)
            await call.ReplyAsync($"[订阅] {alias} 抓取失败。");
        else if (result.SelectedItems == 0)
            await call.ReplyAsync(
                $"[{alias}]{(tag is null ? string.Empty : " tag=" + tag)} 暂无匹配条目（已扫描 {result.FetchedItems} 条）。");
    }

    /// <summary>Items tagged <paramref name="tag"/>; if none are, items whose title or text mention it.</summary>
    private static List<FeedItem> ByTag(IReadOnlyList<FeedItem> items, string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return items.ToList();

        var tagged = items
            .Where(item => item.Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (tagged.Count > 0) return tagged;

        return items
            .Where(item => (item.Title?.Contains(tag, StringComparison.OrdinalIgnoreCase) ?? false) ||
                           (item.Description?.Contains(tag, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();
    }
}
