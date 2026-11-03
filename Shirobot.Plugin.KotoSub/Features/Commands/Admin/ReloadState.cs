namespace Shirobot.Plugin.KotoSub.Features.Commands.Admin;

internal sealed class ReloadState(Func<Task> reload) : ISubCommand
{
    public IReadOnlyList<string> Names { get; } = ["reload"];
    public string Usage => "reload";
    public string Summary => "重新加载配置与状态";
    public CommandAccess Access => CommandAccess.Owner;

    public async Task HandleAsync(CommandCall call)
    {
        try
        {
            await reload();
            await call.ReplyAsync("[订阅] 已重新加载配置与状态。");
        }
        catch (Exception ex)
        {
            await call.ReplyAsync($"[订阅] reload 失败: {ex.Message}");
        }
    }
}
