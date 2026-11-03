using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Shirobot.Plugin.KotoSub.Features.Cards.Generic;

internal sealed partial class ArticleCard : UserControl
{
    public ArticleCard() => AvaloniaXamlLoader.Load(this);
}
