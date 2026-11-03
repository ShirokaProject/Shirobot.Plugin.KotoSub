namespace Shirobot.Plugin.KotoSub.Features.Commands.Contracts;

internal enum CommandAccess
{
    /// <summary>Anyone who can talk to the bot.</summary>
    Anyone,

    /// <summary>Group owner or admin; in private chat the user themselves; always the Bot owner.</summary>
    Manager,

    /// <summary>Bot owner only.</summary>
    Owner
}

/// <summary>One chat sub-command (the word after the prefix). The router checks <see cref="Access"/> first.</summary>
internal interface ISubCommand
{
    IReadOnlyList<string> Names { get; }

    /// <summary>How it is typed after the prefix, e.g. "add &lt;url&gt; [别名]"; shown in help and in usage hints.</summary>
    string Usage { get; }

    /// <summary>One line saying what it does.</summary>
    string Summary { get; }

    CommandAccess Access { get; }
    Task HandleAsync(CommandCall call);
}
