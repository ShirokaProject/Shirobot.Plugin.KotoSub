using System.Globalization;
using System.Xml.Linq;

namespace Shirobot.Plugin.KotoSub.Features.Sources.Rss;

internal static class RssParser
{
    private static readonly XNamespace AtomNs = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace ContentNs = "http://purl.org/rss/1.0/modules/content/";
    private static readonly XNamespace DcNs = "http://purl.org/dc/elements/1.1/";

    private static readonly string[] DateFormats =
    {
        "ddd, dd MMM yyyy HH:mm:ss zzz",
        "ddd, dd MMM yyyy HH:mm:ss 'GMT'",
        "ddd, dd MMM yyyy HH:mm zzz",
        "yyyy-MM-ddTHH:mm:sszzz",
        "yyyy-MM-ddTHH:mm:ss.fffzzz",
        "yyyy-MM-ddTHH:mm:ssZ",
        "yyyy-MM-dd HH:mm:ss"
    };

    /// <summary>Reads an RSS 2.0 / RDF / Atom 1.0 document into feed metadata and items.</summary>
    public static FeedFetchResult Parse(
        XElement? root, string finalUrl, int maxDescription, string? etag, DateTimeOffset? lastModified)
    {
        if (root is null) return new FeedFetchResult(null, []);

        if (string.Equals(root.Name.LocalName, "rss", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(root.Name.LocalName, "RDF", StringComparison.OrdinalIgnoreCase))
        {
            var channel = root.Descendants().FirstOrDefault(e => IsNamed(e, "channel"));
            var imageUrl = channel?.Elements().FirstOrDefault(e => IsNamed(e, "image"))?
                .Elements().FirstOrDefault(e => IsNamed(e, "url"))?.Value;
            return new FeedFetchResult(
                NormalizeTitle(channel?.Elements().FirstOrDefault(e => IsNamed(e, "title"))?.Value?.Trim()),
                ParseRss(root, finalUrl, maxDescription))
            {
                Generator = channel?.Elements().FirstOrDefault(e => IsNamed(e, "generator"))?.Value?.Trim(),
                FeedImageUrl = ResolveFeedImageUrl(imageUrl, finalUrl),
                ETag = etag,
                LastModified = lastModified
            };
        }

        if (string.Equals(root.Name.LocalName, "feed", StringComparison.OrdinalIgnoreCase))
        {
            return new FeedFetchResult(
                NormalizeTitle(root.Element(AtomNs + "title")?.Value?.Trim()),
                ParseAtom(root, finalUrl, maxDescription))
            {
                Generator = root.Element(AtomNs + "generator")?.Value?.Trim(),
                FeedImageUrl = ResolveFeedImageUrl(
                    root.Element(AtomNs + "icon")?.Value ?? root.Element(AtomNs + "logo")?.Value, finalUrl),
                ETag = etag,
                LastModified = lastModified
            };
        }

        throw new InvalidOperationException($"未知的 RSS/Atom 根节点: {root.Name}");
    }

    private static bool IsNamed(XElement element, string localName) =>
        string.Equals(element.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase);

    public static string? NormalizeTitle(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var stripped = HtmlSanitizer.Strip(raw, 0);
        return string.IsNullOrWhiteSpace(stripped) ? null : stripped;
    }

    public static List<FeedItem> ParseRss(XElement root, string baseUrl, int maxDescription)
    {
        var items = new List<FeedItem>();
        var itemElements = root
            .Descendants()
            .Where(e => string.Equals(e.Name.LocalName, "item", StringComparison.OrdinalIgnoreCase));

        foreach (var item in itemElements)
        {
            var titleRaw = ChildValue(item, "title") ?? string.Empty;
            var link = ChildValue(item, "link") ?? string.Empty;
            var guid = ChildValue(item, "guid");
            var pubDateRaw = ChildValue(item, "pubDate") ?? ChildValue(item, "date");
            var dcDate = item.Element(DcNs + "date")?.Value;
            var description = ChildValue(item, "description") ?? string.Empty;
            var contentEncoded = item.Element(ContentNs + "encoded")?.Value;
            var rich = !string.IsNullOrWhiteSpace(contentEncoded) ? contentEncoded! : description;

            var categories = item
                .Elements()
                .Where(e => string.Equals(e.Name.LocalName, "category", StringComparison.OrdinalIgnoreCase))
                .Select(e => e.Value?.Trim() ?? string.Empty)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var publishedRaw = pubDateRaw ?? dcDate;
            var published = TryParseDate(publishedRaw);

            var resolvedLink = ResolveUrl(link, baseUrl);
            var id = !string.IsNullOrWhiteSpace(guid)
                ? guid!
                : !string.IsNullOrWhiteSpace(resolvedLink)
                    ? resolvedLink
                    : titleRaw + "|" + (publishedRaw ?? string.Empty);

            var title = HtmlSanitizer.Strip(titleRaw, 0);
            var safeDescription = HtmlSanitizer.Strip(rich, maxDescription);
            var fullDescription = HtmlSanitizer.StripFormattedText(rich);
            var firstImage = HtmlSanitizer.FindFirstImage(rich, resolvedLink);

            items.Add(new FeedItem(id, title, resolvedLink, safeDescription, published, categories, firstImage)
            {
                FullDescription = fullDescription
            });
        }

        return items;
    }

    public static List<FeedItem> ParseAtom(XElement root, string baseUrl, int maxDescription)
    {
        var items = new List<FeedItem>();
        var entries = root.Elements(AtomNs + "entry");
        foreach (var entry in entries)
        {
            var titleRaw = entry.Element(AtomNs + "title")?.Value ?? string.Empty;
            var id = entry.Element(AtomNs + "id")?.Value ?? string.Empty;
            var updated = entry.Element(AtomNs + "updated")?.Value
                          ?? entry.Element(AtomNs + "published")?.Value;
            var summary = entry.Element(AtomNs + "summary")?.Value;
            var content = entry.Element(AtomNs + "content")?.Value;
            var rich = !string.IsNullOrWhiteSpace(content) ? content! : summary ?? string.Empty;

            var link = entry
                .Elements(AtomNs + "link")
                .FirstOrDefault(l => string.Equals((string?)l.Attribute("rel") ?? "alternate", "alternate", StringComparison.OrdinalIgnoreCase))
                ?.Attribute("href")?.Value
                ?? entry.Elements(AtomNs + "link").FirstOrDefault()?.Attribute("href")?.Value
                ?? string.Empty;

            var categories = entry
                .Elements(AtomNs + "category")
                .Select(e => (string?)e.Attribute("term") ?? e.Value)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var resolvedLink = ResolveUrl(link, baseUrl);
            var published = TryParseDate(updated);
            var effectiveId = !string.IsNullOrWhiteSpace(id)
                ? id
                : !string.IsNullOrWhiteSpace(resolvedLink)
                    ? resolvedLink
                    : titleRaw + "|" + (updated ?? string.Empty);

            var title = HtmlSanitizer.Strip(titleRaw, 0);
            var safeDescription = HtmlSanitizer.Strip(rich, maxDescription);
            var fullDescription = HtmlSanitizer.StripFormattedText(rich);
            var firstImage = HtmlSanitizer.FindFirstImage(rich, resolvedLink);

            items.Add(new FeedItem(effectiveId, title, resolvedLink, safeDescription, published, categories, firstImage)
            {
                FullDescription = fullDescription
            });
        }

        return items;
    }

    private static string? ChildValue(XElement parent, string localName)
    {
        var element = parent
            .Elements()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));
        var value = element?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static DateTimeOffset? TryParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var iso))
        {
            return iso;
        }

        if (DateTimeOffset.TryParseExact(raw, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var exact))
        {
            return exact;
        }

        return null;
    }

    private static string ResolveUrl(string url, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseAbsolute) &&
            Uri.TryCreate(baseAbsolute, url, out var combined))
        {
            return combined.ToString();
        }

        return url;
    }

    private static string? ResolveFeedImageUrl(string? value, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var resolved = ResolveUrl(value.Trim(), baseUrl);
        return Uri.TryCreate(resolved, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? uri.AbsoluteUri
            : null;
    }
}
