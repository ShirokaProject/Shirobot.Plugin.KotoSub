using Xunit;

namespace Shirobot.Plugin.KotoSub.Tests;

public sealed class SubscriptionMigrationTests
{
    [Fact]
    public void LegacyBucketIsInvisibleToInstanceBoundSubscribersUntilExplicitlyAdopted()
    {
        var registry = new SubscriptionRegistry();
        registry.LoadFrom(
            new Dictionary<string, Dictionary<string, string>>
            {
                ["qq|group"] = new() { ["first"] = "first", ["second"] = "second" }
            },
            new Dictionary<string, Dictionary<string, string>>());
        var first = SubscriberKey.Group("qq", "group", "first-instance");
        var second = SubscriberKey.Group("qq", "group", "second-instance");

        Assert.Empty(registry.List(first));
        Assert.Empty(registry.List(second));
        Assert.False(registry.TryGetFeed(first, "first", out _));
        Assert.Null(registry.Remove(first, "first"));
        Assert.Equal(2, registry.List(SubscriberKey.Group("qq", "group")).Count);

        Assert.True(registry.AdoptLegacy(first));
        Assert.Equal(["first", "second"], registry.List(first).Select(s => s.Alias));
        Assert.Empty(registry.List(second));
        Assert.Empty(registry.List(SubscriberKey.Group("qq", "group")));
        Assert.False(registry.AdoptLegacy(second));
    }
}
