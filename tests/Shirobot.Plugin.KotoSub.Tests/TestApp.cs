using ShiroBot.SDK.Plugin;

namespace Shirobot.Plugin.KotoSub.Tests;

internal static class TestApp
{
    /// <summary>The whole plugin wired against a fake HTTP client and a temp directory.</summary>
    public static KotoSubComposition Create(
        string directory, SubState state, KotoSubConfig config, HttpClient http, IBotContext? bot = null) =>
        KotoSubComposition.Create(new CompositionOptions(
            bot!, () => config, _ => { }, () => Task.CompletedTask, http, http, new StateStore(directory), state));
}
