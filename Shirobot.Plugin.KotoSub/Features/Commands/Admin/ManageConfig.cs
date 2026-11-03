using System.Text;

namespace Shirobot.Plugin.KotoSub.Features.Commands.Admin;

/// <summary>"config [key] [value]": view or change plugin settings. Bot owner only: it exposes server details.</summary>
internal sealed class ManageConfig(Func<KotoSubConfig> config, Action<KotoSubConfig> save) : ISubCommand
{
    public IReadOnlyList<string> Names { get; } = ["config", "配置"];
    public string Usage => "config [key] [value]";
    public string Summary => "查看或修改配置";
    public CommandAccess Access => CommandAccess.Owner;

    public async Task HandleAsync(CommandCall call)
    {
        var settings = config();
        if (call.Args.Count == 0 || call.Args[0].Equals("show", StringComparison.OrdinalIgnoreCase))
        {
            await call.ReplyAsync(Show(settings));
            return;
        }

        if (call.Args.Count < 2)
        {
            await call.ReplyAsync(Help());
            return;
        }

        var key = ConfigKeys.Find(call.Args[0]);
        if (key is null)
        {
            await call.ReplyAsync($"[订阅] 未知配置项: {call.Args[0]}\n" + Help());
            return;
        }

        if (!key.TrySet(settings, call.Args[1]))
        {
            await call.ReplyAsync($"[订阅] {key.Name} 的值无效，应为 {key.Hint}。");
            return;
        }

        save(settings);
        await call.ReplyAsync($"[订阅] {key.Name} = {key.Show(settings)}");
    }

    private static string Show(KotoSubConfig settings)
    {
        var builder = new StringBuilder("[订阅] config");
        foreach (var key in ConfigKeys.All)
            builder.Append('\n').Append(key.Name.PadRight(26)).Append("= ").Append(key.Show(settings));
        return builder.ToString();
    }

    private static string Help()
    {
        var builder = new StringBuilder("用法 (Bot 主人):\n#sub config                  查看当前配置\n#sub config <key> <value>   修改配置项\n可用 key:");
        foreach (var key in ConfigKeys.All)
            builder.Append("\n  ").Append(key.Name.PadRight(26)).Append(key.Hint);
        return builder.ToString();
    }
}
