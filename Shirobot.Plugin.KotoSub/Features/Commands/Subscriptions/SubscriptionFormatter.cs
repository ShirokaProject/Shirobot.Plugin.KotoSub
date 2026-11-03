namespace Shirobot.Plugin.KotoSub.Features.Commands.Subscriptions;

internal sealed class SubscriptionFormatter(FeedRegistry feeds)
{
    public string Describe(Subscription subscription, bool showFullUrl, string indent)
    {
        if (!feeds.TryGet(subscription.FeedId, out var feed))
            return $"{indent}{subscription.Alias} (missing)";

        var url = showFullUrl ? feed.Url : FeedUrl.Mask(feed.Url);
        var title = string.IsNullOrWhiteSpace(feed.DisplayName) ? string.Empty : $"  ({feed.DisplayName})";
        return $"{indent}{subscription.Alias}{title}\n    {url}";
    }
}
