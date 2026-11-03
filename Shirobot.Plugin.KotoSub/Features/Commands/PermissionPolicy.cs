using ShiroBot.SDK.Models;
using ShiroBot.SDK.Plugin;

namespace Shirobot.Plugin.KotoSub.Features.Commands;

internal static class PermissionPolicy
{
    public static bool IsOwner(IBotContext context, string senderId) => context.IsAdmin(senderId);

    public static bool CanManage(IBotContext context, MessageEvent message)
    {
        if (message.IsDirect || IsOwner(context, message.Sender.Id)) return true;
        return (message.Member?.Role ?? MemberRole.Member) is MemberRole.Owner or MemberRole.Admin;
    }
}
