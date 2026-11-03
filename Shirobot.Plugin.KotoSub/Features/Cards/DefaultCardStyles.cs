using Shirobot.Plugin.KotoSub.Features.Cards.Bilibili;
using Shirobot.Plugin.KotoSub.Features.Cards.Generic;
using Shirobot.Plugin.KotoSub.Features.Cards.Git;
using Shirobot.Plugin.KotoSub.Features.Cards.WordPress;
using Shirobot.Plugin.KotoSub.Features.Cards.X;

namespace Shirobot.Plugin.KotoSub.Features.Cards;

internal static class DefaultCardStyles
{
    public static CardCatalog Create(HttpClient http) => new(
        [
            new BilibiliCards(new BilibiliVideoDetails(http)),
            new XCards(),
            new GitCards("github", "GitHub", uri => FeedUrl.HostIs(uri, "github.com")),
            new GitCards("gitea", "Gitea", uri =>
                FeedUrl.HostIs(uri, "gitea.com") || uri.IdnHost.StartsWith("git.", StringComparison.OrdinalIgnoreCase)),
            new WordPressCards()
        ],
        new GenericCards());
}
