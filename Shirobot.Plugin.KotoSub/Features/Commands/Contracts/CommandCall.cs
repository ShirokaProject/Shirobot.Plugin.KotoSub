using ShiroBot.SDK.Models;

namespace Shirobot.Plugin.KotoSub.Features.Commands.Contracts;

/// <summary>What the sender may do: manage this chat's subscriptions, or administer the whole plugin.</summary>
internal readonly record struct CallerRights(bool CanManage, bool IsOwner);

/// <summary>Everything a sub-command may know about one incoming command.</summary>
internal sealed class CommandCall(
    MessageEvent message, SubscriberKey scope, CallerRights rights, IReadOnlyList<string> args,
    Func<bool, string, Task> reply)
{
    public MessageEvent Message { get; } = message;

    /// <summary>The only subscriber this command may read or change.</summary>
    public SubscriberKey Scope { get; } = scope;

    /// <summary>Group owner/admin, the user in their own private chat, or the Bot owner.</summary>
    public bool CanManage { get; } = rights.CanManage;

    public bool IsOwner { get; } = rights.IsOwner;

    /// <summary>Arguments after the sub-command word.</summary>
    public IReadOnlyList<string> Args { get; } = args;

    public Task ReplyAsync(string text) => reply(false, text);

    public Task ReplyUsageAsync(ISubCommand command) => reply(false, $"用法: #sub {command.Usage}");

    /// <summary>Replies and, where the platform supports it, mentions the sender.</summary>
    public Task ReplyMentionAsync(string text) => reply(true, text);
}
