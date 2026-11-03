using ShiroBot.Model.QQ;
using ShiroBot.SDK.Models;
using ShiroBot.SDK.Plugin;

namespace Shirobot.Plugin.KotoSub.Features.Delivery;

/// <summary>
/// Sends an item through the QQ official bot API, trying the richest form first:
/// card image, then Markdown (with buttons), then quoted plain text.
/// </summary>
internal sealed class QqOfficialDelivery(IBotContext context, Func<KotoSubConfig> config, CardRenderer? cards)
{
    private const string CardFileName = "kotosub-card.png";

    /// <summary>False when this chat is not served by the QQ official API; the caller then sends generically.</summary>
    public async Task<bool> TrySendAsync(
        SubscriberKey subscriber, string alias, SubscriptionTarget feed, UpdateEvent item, string? replyTo,
        bool includeImage, CancellationToken cancellationToken)
    {
        var official = context.GetAdapterExtension<IQOfficialMessageApi>();
        var isOfficial = string.Equals(subscriber.Platform, "qq-official", StringComparison.OrdinalIgnoreCase);
        var settings = config();
        var target = new QOfficialMessageTarget(
            subscriber.Scope == SubscriberScope.Group ? QOfficialMessageScene.Group : QOfficialMessageScene.Direct,
            subscriber.TargetId);
        var reply = string.IsNullOrWhiteSpace(replyTo) ? null : new QOfficialMessageReply { MessageId = replyTo };

        if (isOfficial && settings.EnableRenderedCards && cards is not null)
        {
            if (official is null || context.GetAdapterExtension<IQOfficialMediaApi>() is null)
                BotLog.Warning("[KotoSub] QQ 官方适配器未暴露统一消息与媒体发送能力，卡片发送回退 Markdown。");
            else if (await TrySendCardAsync(official, target, reply, subscriber, feed, item, includeImage, cancellationToken))
                return true;
        }

        if (official is not null && settings.EnableMarkdown)
        {
            await SendMarkdownAsync(official, target, reply, subscriber, alias, feed, item, includeImage, cancellationToken);
            return true;
        }

        if (!isOfficial) return false;

        await SendQuotedTextAsync(subscriber, feed, item, replyTo ?? throw new InvalidOperationException(
            "QQPlatform text requires a recent incoming message ID."));
        return true;
    }

    private async Task<bool> TrySendCardAsync(
        IQOfficialMessageApi official, QOfficialMessageTarget target, QOfficialMessageReply? reply,
        SubscriberKey subscriber, SubscriptionTarget feed, UpdateEvent item, bool includeImage, CancellationToken cancellationToken)
    {
        var png = await cards!.TryRenderAsync(feed, item, includeImage, cancellationToken).ConfigureAwait(false);
        if (png is not { Length: > 0 })
        {
            BotLog.Warning($"[KotoSub] QQ 卡片渲染无结果 feed={feed.Id}，回退 Markdown。");
            return false;
        }

        var caption = cards.RendersFeedMetadata(feed, item)
            ? null
            : $"来源: {cards.LabelFor(feed, item)}\n发布时间: " +
              (item.Published?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "未知");
        try
        {
            var captionSent = await SendImageAsync(official, target, reply, png, caption, cancellationToken);
            if (!captionSent && !string.IsNullOrWhiteSpace(caption))
                await official.SendAsync(target, QOfficialMessage.Text(caption), reply, cancellationToken)
                    .ConfigureAwait(false);
            BotLog.Info($"[KotoSub] QQ 卡片已上传发送 feed={feed.Id} bytes={png.Length} " +
                        $"caption={captionSent} target={subscriber.Format()}");
            return true;
        }
        catch (NotSupportedException ex)
        {
            BotLog.Warning($"[KotoSub] QQ 官方适配器暂不支持当前会话的媒体发送，回退 Markdown: {ex.Message}");
        }
        catch (Exception ex)
        {
            BotLog.Warning($"[KotoSub] QQ 官方卡片上传发送失败，回退 Markdown: {ex.GetType().Name}: {ex.Message}");
        }

        return false;
    }

    /// <summary>Sends the image, with the caption attached when the platform allows it. Returns true if it did.</summary>
    private static async Task<bool> SendImageAsync(
        IQOfficialMessageApi official, QOfficialMessageTarget target, QOfficialMessageReply? reply,
        byte[] png, string? caption, CancellationToken cancellationToken)
    {
        await using var image = new MemoryStream(png, writable: false);
        if (string.IsNullOrWhiteSpace(caption))
        {
            await official.SendAsync(target, QOfficialMessage.Media(QOfficialMediaType.Image, image, CardFileName),
                reply, cancellationToken).ConfigureAwait(false);
            return false;
        }

        try
        {
            await official.SendAsync(target, QOfficialMessage.Media(QOfficialMediaType.Image, image, CardFileName, caption),
                reply, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (NotSupportedException)
        {
            image.Position = 0;
            await official.SendAsync(target, QOfficialMessage.Media(QOfficialMediaType.Image, image, CardFileName),
                reply, cancellationToken).ConfigureAwait(false);
            return false;
        }
    }

    private async Task SendMarkdownAsync(
        IQOfficialMessageApi official, QOfficialMessageTarget target, QOfficialMessageReply? reply,
        SubscriberKey subscriber, string alias, SubscriptionTarget feed, UpdateEvent item, bool includeImage,
        CancellationToken cancellationToken)
    {
        var settings = config();
        var markdown = new QCustomMarkdown(MarkdownFormatter.Render(feed, alias, item, settings, includeImage));
        var keyboard = MarkdownFormatter.CreateKeyboard(alias, item, settings);
        if (keyboard is not null && official.CanSendMarkdown(target, markdown, keyboard))
        {
            try
            {
                await official.SendAsync(target, QOfficialMessage.Markdown(markdown, keyboard), reply, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
            catch (Exception ex)
            {
                BotLog.Warning($"[KotoSub] QQ 按钮发送失败，重试无按钮 Markdown: {ex.Message}");
            }
        }

        try
        {
            await official.SendAsync(target, QOfficialMessage.Markdown(markdown), reply, cancellationToken)
                .ConfigureAwait(false);
            return;
        }
        catch (Exception ex)
        {
            BotLog.Warning($"[KotoSub] QQ Markdown 发送失败，回退普通文本: {ex.Message}");
        }

        await SendQuotedTextAsync(subscriber, feed, item, reply?.MessageId ?? throw new InvalidOperationException(
            "QQPlatform Markdown failed and no recent message is available for text fallback."));
    }

    private Task SendQuotedTextAsync(SubscriberKey subscriber, SubscriptionTarget feed, UpdateEvent item, string replyTo) =>
        context.Message.SendMessageAsync(
            subscriber.Scope == SubscriberScope.Group
                ? Channel.Group(subscriber.TargetId)
                : Channel.Direct(subscriber.TargetId),
            [new QuoteSegment(replyTo), new TextSegment(ItemText.Preview(feed, item))]);
}
