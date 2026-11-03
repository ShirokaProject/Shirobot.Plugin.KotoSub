using System.Text;
using ShiroBot.SDK.Models;

namespace Shirobot.Plugin.KotoSub.Features.Delivery;

/// <summary>Builds the plain-text and mixed text/image form of an item for adapters without rich messages.</summary>
internal sealed class ItemText(ImageEmbedder images, CardRenderer? cards, Func<KotoSubConfig> config)
{
    public const string Separator = "────────";

    /// <summary>Text only: title, cover link, source name, link and age.</summary>
    public static string Preview(SubscriptionTarget feed, UpdateEvent item)
    {
        var builder = new StringBuilder();
        builder.AppendLine(TitleOf(item));
        if (!string.IsNullOrWhiteSpace(item.FirstImageUrl))
            builder.Append("封面: ").AppendLine(item.FirstImageUrl);
        builder.AppendLine(Separator);
        AppendFooter(builder, feed, item);
        return builder.ToString().TrimEnd();
    }

    /// <summary>Card image when cards are available, else the first picture plus text.</summary>
    public async Task<MessageSegment[]> BuildSegmentsAsync(
        SubscriptionTarget feed, UpdateEvent item, bool includeImage, CancellationToken cancellationToken)
    {
        var tail = new StringBuilder(Separator).AppendLine();
        AppendFooter(tail, feed, item);

        if (config().EnableRenderedCards && cards is not null)
        {
            var png = await cards.TryRenderAsync(feed, item, includeImage, cancellationToken).ConfigureAwait(false);
            if (png is { Length: > 0 })
                return [new ImageSegment("base64://" + Convert.ToBase64String(png)), new TextSegment("\n" + tail)];
        }

        ImageSegment? picture = null;
        if (includeImage && !string.IsNullOrWhiteSpace(item.FirstImageUrl))
            picture = await images.TryBuildAsync(item.FirstImageUrl!, cancellationToken).ConfigureAwait(false);

        return picture is null
            ? [new TextSegment(TitleOf(item) + "\n" + tail)]
            : [new TextSegment(TitleOf(item) + "\n"), picture, new TextSegment("\n" + tail)];
    }

    private static string TitleOf(UpdateEvent item) => string.IsNullOrWhiteSpace(item.Title) ? "(无标题)" : item.Title;

    private static void AppendFooter(StringBuilder builder, SubscriptionTarget feed, UpdateEvent item)
    {
        builder.Append('[').Append(feed.DisplayOrHost).Append(']');
        if (!string.IsNullOrWhiteSpace(item.Link)) builder.AppendLine().Append(item.Link);
        if (item.Published.HasValue)
            builder.AppendLine().Append("发布时间: ").Append(Age(item.Published.Value));
    }

    private static string Age(DateTimeOffset published)
    {
        var local = published.ToLocalTime();
        var delta = DateTimeOffset.Now - local;
        if (delta.TotalSeconds < 0) return local.ToString("yyyy-MM-dd HH:mm");
        if (delta.TotalMinutes < 5) return "[刚刚]";
        if (delta.TotalMinutes < 60) return $"[{(int)delta.TotalMinutes} 分钟前]";
        if (delta.TotalHours < 24) return $"[{(int)delta.TotalHours} 小时前]";
        return local.ToString("yyyy-MM-dd HH:mm");
    }
}
