namespace Shirobot.Plugin.KotoSub.Features.Cards;

/// <summary>Extra card details a platform can add beyond what the feed carries.</summary>
internal sealed record CardDataSupplement(
    string? AuthorName = null,
    string? AvatarUrl = null,
    string? CoverUrl = null,
    string? DurationText = null,
    string? ViewCountText = null,
    string? DanmakuCountText = null,
    string? LikeCountText = null,
    string? CoinCountText = null,
    string? FavoriteCountText = null,
    string? ShareCountText = null,
    string? CategoryText = null,
    string? GitEventTypeText = null,
    string? GitStatusText = null,
    string? GitRepositoryText = null,
    string? GitReferenceText = null);
