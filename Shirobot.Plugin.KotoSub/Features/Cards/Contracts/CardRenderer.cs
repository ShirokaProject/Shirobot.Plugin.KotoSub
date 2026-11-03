using ShiroBot.AvaloniaSdk;
using ShiroBot.SDK.Plugin;

namespace Shirobot.Plugin.KotoSub.Features.Cards.Contracts;

/// <summary>Turns an item into a card image using the templates its platform provides.</summary>
internal sealed class CardRenderer(IRenderContext? renderer, CardCatalog catalog, CardMediaLoader media)
{
    /// <summary>Name of the platform an item belongs to, for captions.</summary>
    public string LabelFor(SubscriptionTarget feed, UpdateEvent item) => catalog.StyleFor(feed, item).Label;

    /// <summary>True when the card already prints the feed's name and icon.</summary>
    public bool RendersFeedMetadata(SubscriptionTarget feed, UpdateEvent item) => catalog.StyleFor(feed, item).RendersFeedMetadata;

    public async Task<byte[]?> TryRenderAsync(
        SubscriptionTarget feed, UpdateEvent item, bool includeCover, CancellationToken cancellationToken)
    {
        if (renderer is not IAvaloniaRenderContext avalonia)
        {
            BotLog.Warning($"[KotoSub] Avalonia 卡片渲染器不可用 feed={feed.Id} renderer={renderer?.GetType().FullName ?? "null"}");
            return null;
        }

        var style = catalog.StyleFor(feed, item);
        var recipe = catalog.RecipeFor(style, feed, item);
        BotLog.Info($"[KotoSub] 选择卡片 feed={feed.Id} style={style.Id} variant={recipe.VariantId}");
        try
        {
            var supplement = await style.LoadDataAsync(item, recipe.VariantId, cancellationToken).ConfigureAwait(false);
            var avatarUrl = supplement?.AvatarUrl ?? feed.SourceImageUrl;
            if (string.IsNullOrWhiteSpace(avatarUrl) && style.ResolvesSiteAvatar)
                avatarUrl = await media.TryResolveSiteAvatarUrlAsync(item.Link, cancellationToken).ConfigureAwait(false);
            using var model = new CardViewModel
            {
                SourceName = style.Label,
                SitePlatformName = style.Label,
                Title = string.IsNullOrWhiteSpace(item.Title) ? "(无标题)" : item.Title,
                Description = BuildCardDescription(item),
                FeedName = feed.DisplayOrHost,
                AuthorName = string.IsNullOrWhiteSpace(supplement?.AuthorName)
                    ? feed.DisplayOrHost
                    : supplement.AuthorName,
                PublishedText = item.Published?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? string.Empty,
                PublishedDateText = item.Published?.ToLocalTime().ToString("MM-dd 发布") ?? string.Empty,
                Link = item.Link,
                TagsText = string.Join("  ", item.Tags.Take(4).Select(tag => "#" + tag)),
                Avatar = await media.TryLoadAsync(avatarUrl, cancellationToken)
                    .ConfigureAwait(false),
                DurationText = supplement?.DurationText ?? string.Empty,
                ViewCountText = supplement?.ViewCountText ?? string.Empty,
                DanmakuCountText = supplement?.DanmakuCountText ?? string.Empty,
                LikeCountText = supplement?.LikeCountText ?? string.Empty,
                CoinCountText = supplement?.CoinCountText ?? string.Empty,
                FavoriteCountText = supplement?.FavoriteCountText ?? string.Empty,
                ShareCountText = supplement?.ShareCountText ?? string.Empty,
                CategoryText = !string.IsNullOrWhiteSpace(supplement?.CategoryText)
                    ? supplement.CategoryText
                    : string.Join(" / ", item.Tags),
                GitEventTypeText = supplement?.GitEventTypeText ?? string.Empty,
                GitStatusText = supplement?.GitStatusText ?? string.Empty,
                GitRepositoryText = supplement?.GitRepositoryText ?? string.Empty,
                GitReferenceText = supplement?.GitReferenceText ?? string.Empty,
                Cover = includeCover
                    ? await media.TryLoadAsync(item.FirstImageUrl ?? supplement?.CoverUrl, cancellationToken)
                        .ConfigureAwait(false)
                    : null
            };
            return await recipe.RenderAsync(avalonia, model, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            BotLog.Warning($"[KotoSub] 卡片渲染失败 feed={feed.Id} style={style.Id} variant={recipe.VariantId}: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static string BuildCardDescription(UpdateEvent item)
    {
        var description = string.IsNullOrWhiteSpace(item.FullDescription)
            ? item.Description
            : item.FullDescription;
        var title = item.Title?.Trim();
        if (!string.IsNullOrWhiteSpace(title) &&
            description.StartsWith(title, StringComparison.OrdinalIgnoreCase))
            description = description[title.Length..].TrimStart(' ', '\r', '\n', '\t', '·', '-', '—');
        return description;
    }
}
