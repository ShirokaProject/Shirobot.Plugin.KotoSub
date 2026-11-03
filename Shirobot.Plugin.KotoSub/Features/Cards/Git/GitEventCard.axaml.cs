using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Shirobot.Plugin.KotoSub.Features.Cards.Git;

internal sealed partial class GitEventCard : UserControl
{
    public GitEventCard() => AvaloniaXamlLoader.Load(this);
}
