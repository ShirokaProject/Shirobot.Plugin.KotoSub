using ShiroBot.SDK.Models;
using ShiroBot.SDK.Plugin;

namespace Shirobot.Plugin.KotoSub.Features.Commands;

/// <summary>Turns a chat message into a <see cref="CommandCall"/> for exactly one subscriber and runs the command.</summary>
internal sealed class SubCommandRouter
{
    public static readonly string[] Prefixes = ["#sub", "#rss"];

    private readonly IBotContext _bot;
    private readonly Dictionary<string, ISubCommand> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ISubCommand> _all;
    private readonly SubscriptionRegistry _subscriptions;
    private readonly StatePersister _persister;

    public SubCommandRouter(
        IBotContext bot, IEnumerable<ISubCommand> commands, SubscriptionRegistry subscriptions, StatePersister persister)
    {
        _bot = bot;
        _subscriptions = subscriptions;
        _persister = persister;
        _all = commands.ToList();
        foreach (var command in _all)
        {
            foreach (var name in command.Names) _commands[name] = command;
        }
    }

    public Task HandleGroupAsync(MessageEvent message) =>
        HandleAsync(message, SubscriberKey.Group(message.Platform, message.Channel.Id, message.InstanceId));

    public Task HandleDirectAsync(MessageEvent message) =>
        HandleAsync(message, SubscriberKey.Friend(message.Platform, message.Sender.Id, message.InstanceId));

    /// <summary>True for "#sub" / "#rss" followed by nothing or whitespace, so "#subway" is left alone.</summary>
    public static bool IsCommand(string text) => TryStrip(text, out _);

    internal static MessageSegment[] BuildReplySegments(
        MessageEvent message, bool mention, string text, IEnumerable<MessageSegment>? extra = null)
    {
        var segments = new List<MessageSegment>();
        // QQPlatform renders MentionSegment as plain "@" + OpenID; the reply already targets the incoming message.
        if (mention && !string.Equals(message.Platform, "qq-official", StringComparison.OrdinalIgnoreCase))
            segments.Add(new MentionSegment(message.Sender.Id));
        segments.Add(new TextSegment(segments.Count > 0 ? " " + text : text));
        if (extra is not null) segments.AddRange(extra);
        return [.. segments];
    }

    private async Task HandleAsync(MessageEvent message, SubscriberKey scope)
    {
        if (!TryStrip(message.GetPlainText(), out var rest)) return;

        var tokens = rest.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var isOwner = PermissionPolicy.IsOwner(_bot, message.Sender.Id);
        var canManage = PermissionPolicy.CanManage(_bot, message);
        var word = tokens.Length == 0 ? "status" : tokens[0];
        var call = new CommandCall(message, scope, new CallerRights(canManage, isOwner), tokens.Skip(1).ToArray(),
            (mention, text) => _bot.Message.QuoteReplyAsync(
                message, BuildReplySegments(message, mention && !message.IsDirect, text)));

        AdoptLegacySubscriptions(message, scope);

        if (!_commands.TryGetValue(word, out var command))
        {
            await call.ReplyAsync("未知子命令。\n" + ShowHelp.TextFor(_all));
            return;
        }

        var allowed = command.Access switch
        {
            CommandAccess.Owner => isOwner,
            CommandAccess.Manager => canManage,
            _ => true
        };
        if (!allowed)
        {
            await call.ReplyMentionAsync(command.Access == CommandAccess.Owner
                ? $"[订阅] 仅 Bot 主人可执行 {word}。"
                : $"[订阅] 仅群管理员或 Bot 主人可执行 {word}。");
            return;
        }

        await command.HandleAsync(call);
    }

    private static bool TryStrip(string text, out string rest)
    {
        var trimmed = text?.Trim() ?? string.Empty;
        foreach (var prefix in Prefixes)
        {
            if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            if (trimmed.Length > prefix.Length && !char.IsWhiteSpace(trimmed[prefix.Length])) continue;
            rest = trimmed[prefix.Length..].Trim();
            return true;
        }

        rest = string.Empty;
        return false;
    }

    // Subscriptions saved before adapter instances were recorded belong to a scope only when exactly one
    // instance serves that platform; with several instances ownership is ambiguous, so they stay untouched.
    private void AdoptLegacySubscriptions(MessageEvent message, SubscriberKey scope)
    {
        if (message.InstanceId is null) return;
        var sameInstancePlatform = _bot.GetAdapterInstances().Count(instance =>
            string.Equals(instance.Platform, message.Platform, StringComparison.OrdinalIgnoreCase));
        if (sameInstancePlatform == 1 && _subscriptions.AdoptLegacy(scope))
            _persister.PersistSync();
    }
}
