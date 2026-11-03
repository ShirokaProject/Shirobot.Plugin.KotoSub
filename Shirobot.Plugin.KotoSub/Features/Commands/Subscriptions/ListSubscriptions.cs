using System.Text;

namespace Shirobot.Plugin.KotoSub.Features.Commands.Subscriptions;

internal sealed class ListSubscriptions(SubscriptionRegistry subscriptions, SubscriptionFormatter formatter)
    : ISubCommand
{
    public IReadOnlyList<string> Names { get; } = ["list"];
    public string Usage => "list";
    public string Summary => "列出当前会话的订阅";
    public CommandAccess Access => CommandAccess.Anyone;

    public async Task HandleAsync(CommandCall call)
    {
        var mine = subscriptions.List(call.Scope);
        if (mine.Count == 0)
        {
            await call.ReplyAsync("[订阅] 当前会话没有订阅。");
            return;
        }

        var builder = new StringBuilder();
        builder.Append("[订阅] 当前订阅 (").Append(mine.Count).AppendLine(")");
        foreach (var subscription in mine)
            builder.AppendLine(formatter.Describe(subscription, call.CanManage, "- "));

        await call.ReplyAsync(builder.ToString().TrimEnd());
    }
}
