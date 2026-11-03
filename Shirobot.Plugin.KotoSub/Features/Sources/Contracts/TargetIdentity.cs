using System.Security.Cryptography;
using System.Text;

namespace Shirobot.Plugin.KotoSub.Features.Sources.Contracts;

internal static class TargetIdentity
{
    public static string IdFor(string providerId, string key)
    {
        // Keep existing RSS ids stable so subscriber aliases and delivery receipts remain valid.
        if (providerId.Equals("rss", StringComparison.OrdinalIgnoreCase))
            return FeedUrl.KeyOf(key);

        var identity = Encoding.UTF8.GetBytes(providerId + "\0" + key);
        var digest = Convert.ToHexString(SHA256.HashData(identity), 0, 8).ToLowerInvariant();
        var provider = FeedIdGenerator.Sanitize(providerId);
        return $"t-{provider}-{digest}";
    }
}
