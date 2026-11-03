namespace Shirobot.Plugin.KotoSub.Features.Commands;

internal sealed class ShowHelp(IReadOnlyList<ISubCommand> commands) : ISubCommand
{
    public IReadOnlyList<string> Names { get; } = ["help", "?", "用法", "帮助"];
    public string Usage => "help";
    public string Summary => "查看本帮助";
    public CommandAccess Access => CommandAccess.Anyone;

    public static string TextFor(IEnumerable<ISubCommand> commands)
    {
        var rows = commands.ToList();
        var width = rows.Max(command => command.Usage.Length);
        var lines = rows.Select(command =>
            $"#sub {command.Usage.PadRight(width)}  {command.Summary}{Who(command.Access)}");
        return "用法:\n" + string.Join("\n", lines) + "\n（#rss 与 #sub 等价）";
    }

    public Task HandleAsync(CommandCall call) => call.ReplyAsync(TextFor(commands));

    private static string Who(CommandAccess access) => access switch
    {
        CommandAccess.Manager => "（群管理员/Bot 主人）",
        CommandAccess.Owner => "（Bot 主人）",
        _ => string.Empty
    };
}
