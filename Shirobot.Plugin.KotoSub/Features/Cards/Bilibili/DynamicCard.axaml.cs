using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Shirobot.Plugin.KotoSub.Features.Cards.Bilibili;

internal sealed partial class DynamicCard : UserControl
{
    public DynamicCard() => AvaloniaXamlLoader.Load(this);
}
