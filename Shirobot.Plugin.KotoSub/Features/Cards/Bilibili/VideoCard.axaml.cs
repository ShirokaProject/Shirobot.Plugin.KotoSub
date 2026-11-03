using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Shirobot.Plugin.KotoSub.Features.Cards.Bilibili;

internal sealed partial class VideoCard : UserControl
{
    public VideoCard() => AvaloniaXamlLoader.Load(this);
}
