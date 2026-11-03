using System.Security.Cryptography;
using System.Text;

namespace Shirobot.Plugin.KotoSub.Features.Sources.Contracts;

/// <summary>Everything the plugin needs to know about a feed address: identity, comparison, display.</summary>
internal static class FeedUrl
{
    public static string KeyOf(string url)
    {
        var canonical = Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? $"{uri.Scheme.ToLowerInvariant()}://{uri.UserInfo}@{uri.IdnHost.ToLowerInvariant()}:{uri.Port}{uri.PathAndQuery}"
            : url;
        return "f-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)), 0, 6).ToLowerInvariant();
    }

    /// <summary>Scheme, host and port compare case-insensitively; path and query are case-sensitive.</summary>
    public static bool Same(string left, string right)
    {
        if (!Uri.TryCreate(left, UriKind.Absolute, out var a) || !Uri.TryCreate(right, UriKind.Absolute, out var b))
            return string.Equals(left, right, StringComparison.Ordinal);

        return string.Equals(a.Scheme, b.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.IdnHost, b.IdnHost, StringComparison.OrdinalIgnoreCase)
            && a.Port == b.Port
            && string.Equals(a.UserInfo, b.UserInfo, StringComparison.Ordinal)
            && string.Equals(a.PathAndQuery, b.PathAndQuery, StringComparison.Ordinal);
    }

    public static bool HostIs(Uri uri, string domain) =>
        uri.IdnHost.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
        uri.IdnHost.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);

    public static string HostOf(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.IdnHost : "feed";

    /// <summary>Scheme and host only: paths, queries and credentials can carry private tokens.</summary>
    public static string Mask(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return "(已隐藏)";
        var hasMore = uri.PathAndQuery.Length > 1 || !string.IsNullOrEmpty(uri.UserInfo);
        return $"{uri.Scheme}://{uri.IdnHost}{(hasMore ? "/…" : string.Empty)}";
    }
}
