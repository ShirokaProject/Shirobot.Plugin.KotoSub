using ShiroBot.SDK.Models;
using ShiroBot.SDK.Plugin;

namespace Shirobot.Plugin.KotoSub.Features.Delivery.Contracts;

/// <summary>Sends one item to one subscriber on the right adapter instance, in the best form that chat supports.</summary>
internal sealed class ItemDispatcher(
    IBotContext context, Func<KotoSubConfig> config, QqOfficialDelivery qq, ItemText text)
{
    private const int RecentCapacity = 4096;

    private readonly Dictionary<SubscriberKey, (string MessageId, DateTimeOffset SeenAt)> _recent = [];
    private readonly Queue<SubscriberKey> _recentOrder = [];

    /// <summary>Remembers the newest incoming message per chat: official QQ bots may only reply to recent ones.</summary>
    public void RegisterIncoming(MessageEvent message)
    {
        var key = message.IsDirect
            ? SubscriberKey.Friend(message.Platform, message.Channel.Id, message.InstanceId)
            : SubscriberKey.Group(message.Platform, message.Channel.Id, message.InstanceId);
        lock (_recent)
        {
            if (!_recent.ContainsKey(key)) _recentOrder.Enqueue(key);
            _recent[key] = (message.MessageId, DateTimeOffset.UtcNow);
            while (_recentOrder.Count > RecentCapacity) _recent.Remove(_recentOrder.Dequeue());
        }
    }

    /// <summary>Scheduled push: replies to the chat's latest message when the platform needs one.</summary>
    public Task<bool> PushItemAsync(SubscriberKey subscriber, string alias, FeedSource feed, FeedItem item) =>
        SendItemAsync(subscriber, alias, feed, item, RecentMessageId(subscriber), config().IncludeImage);

    public async Task<bool> SendItemAsync(
        SubscriberKey subscriber, string alias, FeedSource feed, FeedItem item, string? replyToMessageId,
        bool includeImage, CancellationToken cancellationToken = default)
    {
        try
        {
            using var instance = context.UseInstance(ResolveInstanceId(subscriber));
            if (await qq.TrySendAsync(subscriber, alias, feed, item, replyToMessageId, includeImage, cancellationToken)
                    .ConfigureAwait(false))
                return true;

            var segments = await text.BuildSegmentsAsync(feed, item, includeImage, cancellationToken).ConfigureAwait(false);
            if (replyToMessageId is not null)
                segments = [new QuoteSegment(replyToMessageId), .. segments];
            await context.Message.SendMessageAsync(
                subscriber.Scope == SubscriberScope.Group
                    ? Channel.Group(subscriber.TargetId)
                    : Channel.Direct(subscriber.TargetId),
                segments).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            BotLog.Warning($"[KotoSub] 推送到 {subscriber.Format()} feed={feed.Id} 失败: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    // Subscriptions saved before adapter instances were recorded carry no instance: usable only if one adapter
    // serves that platform.
    private string ResolveInstanceId(SubscriberKey subscriber)
    {
        if (!string.IsNullOrWhiteSpace(subscriber.InstanceId)) return subscriber.InstanceId;

        var matches = context.GetAdapterInstances()
            .Where(instance => string.Equals(instance.Platform, subscriber.Platform, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return matches.Length == 1
            ? matches[0].Id
            : throw new InvalidOperationException(matches.Length == 0
                ? $"未找到平台 {subscriber.Platform} 的适配器实例。"
                : $"旧订阅未记录适配器实例，平台 {subscriber.Platform} 当前有 {matches.Length} 个实例；请在目标实例中重新订阅。");
    }

    private string? RecentMessageId(SubscriberKey key)
    {
        lock (_recent)
        {
            if (!_recent.TryGetValue(key, out var entry)) return null;
            var lifetime = key.Scope == SubscriberScope.Group ? TimeSpan.FromMinutes(4) : TimeSpan.FromMinutes(55);
            return DateTimeOffset.UtcNow - entry.SeenAt < lifetime ? entry.MessageId : null;
        }
    }
}
