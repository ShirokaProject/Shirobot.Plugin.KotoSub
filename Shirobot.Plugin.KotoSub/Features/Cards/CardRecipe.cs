using Avalonia.Controls;
using ShiroBot.AvaloniaSdk;

namespace Shirobot.Plugin.KotoSub.Features.Cards;

internal interface ICardRecipe
{
    string VariantId { get; }
    Task<byte[]> RenderAsync(IAvaloniaRenderContext renderer, CardViewModel model, CancellationToken cancellationToken);
}

/// <summary>Renders one card template (an Avalonia control) to a PNG.</summary>
internal sealed class CardRecipe<TControl>(string variantId) : ICardRecipe
    where TControl : Control, new()
{
    public string VariantId { get; } = variantId;

    public Task<byte[]> RenderAsync(IAvaloniaRenderContext renderer, CardViewModel model,
        CancellationToken cancellationToken) =>
        renderer.RenderControlPngAsync<TControl>(model, new ControlRenderOptions(Dpi: 192), cancellationToken);
}
