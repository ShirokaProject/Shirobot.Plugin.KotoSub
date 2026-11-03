namespace Shirobot.Plugin.KotoSub.Features.Sources.Contracts;

internal static class FetchLog
{
    public static string Describe(Exception exception)
    {
        var detail = $"{exception.GetType().Name}: {exception.Message}";
        if (exception.InnerException is { } inner)
            detail += $"; inner={inner.GetType().Name}: {inner.Message}";
        return detail.Replace('\r', ' ').Replace('\n', ' ');
    }
}
