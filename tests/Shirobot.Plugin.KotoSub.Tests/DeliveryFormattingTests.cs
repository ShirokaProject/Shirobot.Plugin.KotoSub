using ShiroBot.Model.QQ;
using ShiroBot.SDK.Models;
using Xunit;

namespace Shirobot.Plugin.KotoSub.Tests;

public sealed class DeliveryFormattingTests
{
    [Fact]
    public void QQPlatformRepliesDoNotRenderOpenIdAsMention()
    {
        var message = new MessageEvent
        {
            Platform = "qq-official",
            MessageId = "incoming",
            Channel = Channel.Group("group-openid"),
            Sender = new User("7C776D0A8A0B3C63156F4724DCCC311F"),
            Segments = [new TextSegment("#rss add")]
        };
        var segments = SubCommandRouter.BuildReplySegments(message, true, "[RSS] 已添加并订阅。");
        Assert.Equal("[RSS] 已添加并订阅。", Assert.Single(segments.OfType<TextSegment>()).Text);
        Assert.DoesNotContain(segments, segment => segment is MentionSegment);

        var milky = SubCommandRouter.BuildReplySegments(message with { Platform = "qq" }, true, "完成");
        Assert.Equal(message.Sender.Id, Assert.Single(milky.OfType<MentionSegment>()).UserId);
    }

    [Fact]
    public void OpenIdSubscribersSurviveStateRoundTrip()
    {
        var registry = new SubscriptionRegistry();
        var key = SubscriberKey.Group("qq-official", "FDC67211C475FC5275F32B3E92430889");
        registry.Add(key, "my-news", "news");
        var saved = registry.Snapshot().Groups;
        var restored = new SubscriptionRegistry();
        restored.LoadFrom(saved, new Dictionary<string, Dictionary<string, string>>());
        Assert.Equal(new Subscriber(key, "my-news"), Assert.Single(restored.SubscribersOf("news")));
        Assert.Equal([key.StorageKey], saved.Keys);
        Assert.Throws<FormatException>(() => SubscriberKey.FromStorageKey(SubscriberScope.Group, "12345"));
    }

    [Fact]
    public void MarkdownTemplateRendersFeedFieldsAndSafeButtons()
    {
        var config = new KotoSubConfig { IncludeImage = true };
        var feed = new FeedSource { Id = "news", DisplayName = "技术*周刊" };
        var item = new FeedItem("1", "A [title]", "https://example.org/post", "正文", null, [], "https://example.org/a.png");
        var markdown = MarkdownFormatter.Render(feed, "my-news", item, config, includeImage: true);
        Assert.Contains("A \\[title\\]", markdown);
        Assert.Contains("技术\\*周刊", markdown);
        Assert.Contains("![封面](https://example.org/a.png)", markdown);
        Assert.Contains("[阅读原文](https://example.org/post)", markdown);
        var buttons = Assert.Single(MarkdownFormatter.CreateKeyboard("my-news", item, config)!.Rows).Buttons;
        Assert.Equal(QKeyboardActionType.Jump, buttons[0].Action.Type);
        Assert.Equal(QKeyboardActionType.Command, buttons[1].Action.Type);
        Assert.Equal("#sub latest my-news", buttons[1].Action.Data);
    }

    [Fact]
    public void UnsafeArticleUrlDoesNotBecomeActionButton()
    {
        var config = new KotoSubConfig();
        var feed = new FeedSource { Id = "news" };
        var item = new FeedItem("1", "title", "javascript:alert(1)", "", null, [], null);
        var buttons = Assert.Single(MarkdownFormatter.CreateKeyboard("news", item, config)!.Rows).Buttons;
        Assert.Single(buttons);
        Assert.Equal(QKeyboardActionType.Command, buttons[0].Action.Type);
        Assert.DoesNotContain("javascript:", MarkdownFormatter.Render(feed, "news", item, config, includeImage: false));
    }
}
