namespace Shirobot.Plugin.KotoSub.Features.Polling;

internal static class BackoffPolicy
{
    public static TimeSpan EffectiveInterval(FeedSource feed, KotoSubConfig config)
    {
        var baseInterval = Math.Max(1, Math.Max(
            config.MinIntervalSeconds,
            feed.IntervalSeconds ?? config.DefaultIntervalSeconds));

        if (feed.ConsecutiveFailures <= 0)
        {
            return TimeSpan.FromSeconds(baseInterval);
        }

        var backoffMultiplier = Math.Min(1L << Math.Min(feed.ConsecutiveFailures, 10), 1024);
        var seconds = Math.Min((long)baseInterval * backoffMultiplier,
            Math.Max(baseInterval, config.BackoffMaxSeconds));
        return TimeSpan.FromSeconds(seconds);
    }

    public static DateTimeOffset NextDueAt(FeedSource feed, KotoSubConfig config)
    {
        if (feed.LastFetchAt is null)
        {
            return DateTimeOffset.UtcNow;
        }

        return feed.LastFetchAt.Value + EffectiveInterval(feed, config);
    }
}
