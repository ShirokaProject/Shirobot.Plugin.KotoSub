using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Shirobot.Plugin.KotoSub.Features.Cards.WordPress;

internal sealed partial class WordPressArticleCard : UserControl
{
    public WordPressArticleCard() => AvaloniaXamlLoader.Load(this);
}
