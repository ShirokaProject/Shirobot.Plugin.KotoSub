namespace Shirobot.Plugin.KotoSub.Features.Commands.Subscriptions;

/// <summary>Renames one of this chat's own aliases; other chats' aliases for the same feed are unaffected.</summary>
internal sealed class RenameAlias(SubscriptionRegistry subscriptions, StatePersister persister) : ISubCommand
{
    public IReadOnlyList<string> Names { get; } = ["rename"];
    public string Usage => "rename <旧别名> <新别名>";
    public string Summary => "重命名别名（仅当前会话生效）";
    public CommandAccess Access => CommandAccess.Manager;

    public async Task HandleAsync(CommandCall call)
    {
        if (call.Args.Count < 2)
        {
            await call.ReplyUsageAsync(this);
            return;
        }

        var oldAlias = FeedIdGenerator.Sanitize(call.Args[0]);
        var newAlias = FeedIdGenerator.Sanitize(call.Args[1]);
        if (string.Equals(oldAlias, newAlias, StringComparison.OrdinalIgnoreCase))
        {
            await call.ReplyAsync("[订阅] 新旧别名相同，无需重命名。");
            return;
        }

        if (!subscriptions.TryGetFeed(call.Scope, oldAlias, out _))
        {
            await call.ReplyMentionAsync($"[订阅] 当前会话未订阅 {oldAlias}。");
            return;
        }

        if (!subscriptions.RenameAlias(call.Scope, oldAlias, newAlias))
        {
            await call.ReplyMentionAsync($"[订阅] 别名 {newAlias} 已被当前会话使用，请换一个。");
            return;
        }

        persister.PersistSync();
        await call.ReplyAsync($"[订阅] 已将 {oldAlias} 重命名为 {newAlias}。");
    }
}
