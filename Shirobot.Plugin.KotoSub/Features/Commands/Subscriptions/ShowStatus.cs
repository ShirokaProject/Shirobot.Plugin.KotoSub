using System.Text;

namespace Shirobot.Plugin.KotoSub.Features.Commands.Subscriptions;

internal sealed class ShowStatus(
    SubscriptionRegistry subscriptions, SubscriptionFormatter formatter, Func<KotoSubConfig> config) : ISubCommand
{
    public IReadOnlyList<string> Names { get; } = ["status", "状态"];
    public string Usage => "status";
    public string Summary => "查看当前会话状态";
    public CommandAccess Access => CommandAccess.Anyone;

    public async Task HandleAsync(CommandCall call)
    {
        var settings = config();
        var mine = subscriptions.List(call.Scope);

        var builder = new StringBuilder();
        builder.AppendLine("[订阅] status");
        builder.AppendLine($"enabled: {(settings.Enabled ? "on" : "off")}");
        builder.AppendLine($"scope: {call.Scope.Format()}");
        builder.AppendLine($"subscriptions: {mine.Count}");
        if (call.IsOwner)
        {
            builder.AppendLine($"default_interval: {settings.DefaultIntervalSeconds}s");
            builder.AppendLine($"min_interval: {settings.MinIntervalSeconds}s");
            builder.AppendLine($"max_items_per_push: {settings.MaxItemsPerPush}");
            builder.AppendLine($"rendered_cards: {(settings.EnableRenderedCards ? "on" : "off")}");
            builder.AppendLine($"allow_private_urls: {(settings.AllowPrivateUrls ? "on" : "off")}");
        }

        foreach (var subscription in mine)
            builder.AppendLine(formatter.Describe(subscription, call.CanManage, "  - "));

        await call.ReplyAsync(builder.ToString().TrimEnd());
    }
}
