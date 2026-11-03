using ShiroBot.SDK.Models;
using Xunit;

namespace Shirobot.Plugin.KotoSub.Tests;

public sealed class CommandRouterTests : IDisposable
{
    private readonly SubscriptionIsolationTests.Harness _h = new();

    public void Dispose() => _h.Dispose();

    [Theory]
    [InlineData("#sub list")]
    [InlineData("#rss list")]
    [InlineData("#SUB list")]
    [InlineData("  #sub   list  ")]
    public async Task BothPrefixesAreAcceptedInAnyCase(string text) =>
        Assert.Contains("没有订阅", await _h.Say(text));

    [Theory]
    [InlineData("#subway")]
    [InlineData("#rssfeed list")]
    [InlineData("sub list")]
    public async Task OtherTextIsLeftAlone(string text) => Assert.Equal(string.Empty, await _h.Say(text));

    [Fact]
    public async Task BareCommandShowsStatus() =>
        Assert.Contains("scope:", await _h.Say("#sub"));

    [Fact]
    public async Task HelpIsBuiltFromTheCommandsThemselves()
    {
        var help = await _h.Say("#sub help");

        foreach (var usage in new[] { "add <url> [别名]", "remove <别名>", "rename <旧别名> <新别名>",
                     "latest <别名> [tag] [n=N]", "test <别名>", "list", "status", "config [key] [value]",
                     "interval <别名> <sec>", "reload", "feeds" })
            Assert.Contains("#sub " + usage, help);
        Assert.Contains("（群管理员/Bot 主人）", help);
        Assert.Contains("（Bot 主人）", help);
    }

    [Fact]
    public async Task UnknownSubCommandExplainsWhatExists()
    {
        var reply = await _h.Say("#sub frobnicate");

        Assert.StartsWith("未知子命令", reply);
        Assert.Contains("#sub add", reply);
    }

    [Fact]
    public async Task PlainMembersCannotManageButCanLook()
    {
        var add = await _h.Say("#sub add https://example.test/feed", sender: "plain", role: MemberRole.Member);

        Assert.Contains("仅群管理员或 Bot 主人", add);
        Assert.Contains("没有订阅", await _h.Say("#sub list", sender: "plain", role: MemberRole.Member));
    }

    [Fact]
    public async Task GroupAdminsCannotRunOwnerCommands()
    {
        foreach (var command in new[] { "config", "feeds", "reload", "interval x 10" })
            Assert.Contains("仅 Bot 主人", await _h.Say("#sub " + command, role: MemberRole.Admin));
    }

    [Fact]
    public async Task OwnersCanRunEverything()
    {
        Assert.Contains("user_agent", await _h.Say("#sub config", sender: SubscriptionIsolationTests.Harness.Owner));
        Assert.Contains("没有订阅源", await _h.Say("#sub feeds", sender: SubscriptionIsolationTests.Harness.Owner));
    }

    [Fact]
    public async Task OwnerCanChangeASettingFromChat()
    {
        var owner = SubscriptionIsolationTests.Harness.Owner;

        Assert.Contains("max_items_per_push = 7", await _h.Say("#sub config max_items 7", sender: owner));
        Assert.Contains("max_items_per_push", await _h.Say("#sub config max_items zero", sender: owner));
        Assert.Contains("= 7", await _h.Say("#sub config", sender: owner));
    }

    [Fact]
    public async Task PrivateChatUserManagesTheirOwnSubscriptions()
    {
        var reply = await _h.Say("#sub add https://example.test/feed mine", target: "42", sender: "42", group: false);

        Assert.Contains("已订阅 mine", reply);
    }
}
