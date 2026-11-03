namespace Shirobot.Plugin.KotoSub.Features.Commands.Subscriptions;

/// <summary>Aliases are unique only inside one subscriber; nothing here looks at other subscribers.</summary>
internal static class AliasPolicy
{
    public static string Choose(
        SubscriptionRegistry subscriptions, SubscriberKey scope, string? suggested, string url)
    {
        var baseAlias = !string.IsNullOrWhiteSpace(suggested) ? suggested : FeedIdGenerator.Derive(url);
        return FeedIdGenerator.EnsureUnique(baseAlias, candidate => subscriptions.TryGetFeed(scope, candidate, out _));
    }

    public static bool IsTaken(SubscriptionRegistry subscriptions, SubscriberKey scope, string alias) =>
        subscriptions.TryGetFeed(scope, alias, out _);
}
