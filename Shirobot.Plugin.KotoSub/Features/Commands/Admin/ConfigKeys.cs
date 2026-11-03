using System.Globalization;

namespace Shirobot.Plugin.KotoSub.Features.Commands.Admin;

/// <summary>One editable setting: how it is named, shown and parsed.</summary>
internal sealed record ConfigKey(
    string Name, string[] Aliases, string Hint, Func<KotoSubConfig, string> Show,
    Func<KotoSubConfig, string, bool> TrySet);

/// <summary>The settings that can be changed from chat, described once instead of one switch case each.</summary>
internal static class ConfigKeys
{
    public static readonly IReadOnlyList<ConfigKey> All =
    [
        Flag("enabled", ["插件"], c => c.Enabled, (c, v) => c.Enabled = v),
        Count("default_interval_seconds", ["default_interval", "interval"], c => c.DefaultIntervalSeconds,
            (c, v) => c.DefaultIntervalSeconds = Math.Max(v, c.MinIntervalSeconds)),
        Count("min_interval_seconds", ["min_interval"], c => c.MinIntervalSeconds, (c, v) => c.MinIntervalSeconds = v),
        Count("request_timeout_seconds", ["request_timeout", "timeout"], c => c.RequestTimeoutSeconds,
            (c, v) => c.RequestTimeoutSeconds = v),
        Count("max_items_per_push", ["max_items"], c => c.MaxItemsPerPush, (c, v) => c.MaxItemsPerPush = v),
        Count("max_description_length", ["max_desc"], c => c.MaxDescriptionLength,
            (c, v) => c.MaxDescriptionLength = v),
        Count("latest_max_n", [], c => c.LatestMaxN, (c, v) => c.LatestMaxN = v),
        Flag("include_image", ["图片"], c => c.IncludeImage, (c, v) => c.IncludeImage = v),
        Flag("enable_rendered_cards", ["rendered_cards"], c => c.EnableRenderedCards,
            (c, v) => c.EnableRenderedCards = v),
        Flag("enable_markdown", [], c => c.EnableMarkdown, (c, v) => c.EnableMarkdown = v),
        Flag("enable_action_buttons", [], c => c.EnableActionButtons, (c, v) => c.EnableActionButtons = v),
        Flag("allow_private_urls", ["private"], c => c.AllowPrivateUrls, (c, v) => c.AllowPrivateUrls = v),
        new ConfigKey("user_agent", [], "<ua>", c => c.UserAgent, (c, v) =>
        {
            if (string.IsNullOrWhiteSpace(v)) return false;
            c.UserAgent = v;
            return true;
        }),
        Count("last_seen_capacity", [], c => c.LastSeenCapacity, (c, v) => c.LastSeenCapacity = v),
        Count("backoff_max_seconds", ["backoff_max"], c => c.BackoffMaxSeconds, (c, v) => c.BackoffMaxSeconds = v)
    ];

    public static ConfigKey? Find(string name) => All.FirstOrDefault(key =>
        key.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
        key.Aliases.Any(alias => alias.Equals(name, StringComparison.OrdinalIgnoreCase)));

    private static ConfigKey Flag(string name, string[] aliases, Func<KotoSubConfig, bool> get, Action<KotoSubConfig, bool> set) =>
        new(name, aliases, "on|off", c => get(c) ? "on" : "off", (c, text) =>
        {
            switch (text.Trim().ToLowerInvariant())
            {
                case "on" or "true" or "1" or "yes" or "enable" or "enabled" or "开启" or "打开" or "启用":
                    set(c, true);
                    return true;
                case "off" or "false" or "0" or "no" or "disable" or "disabled" or "关闭" or "关" or "禁用":
                    set(c, false);
                    return true;
                default:
                    return false;
            }
        });

    private static ConfigKey Count(string name, string[] aliases, Func<KotoSubConfig, int> get, Action<KotoSubConfig, int> set) =>
        new(name, aliases, "<n>", c => get(c).ToString(CultureInfo.InvariantCulture), (c, text) =>
        {
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value <= 0)
                return false;
            set(c, value);
            return true;
        });
}
