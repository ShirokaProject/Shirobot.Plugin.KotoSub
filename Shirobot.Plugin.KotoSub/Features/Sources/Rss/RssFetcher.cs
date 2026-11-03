using System.Net.Http.Headers;
using System.Net;
using System.Xml.Linq;
using System.Xml;

namespace Shirobot.Plugin.KotoSub.Features.Sources.Rss;

internal sealed class RssFetcher
{

    private readonly HttpClient _httpClient;
    private readonly Func<KotoSubConfig> _configAccessor;
    private KotoSubConfig Config => _configAccessor();

    public RssFetcher(HttpClient httpClient, Func<KotoSubConfig> configAccessor)
    {
        _httpClient = httpClient;
        _configAccessor = configAccessor;
    }

    public Task<FeedFetchResult> FetchAsync(string url, CancellationToken cancellationToken) =>
        FetchAsync(url, null, null, cancellationToken);

    public async Task<FeedFetchResult> FetchAsync(
        string url, string? etag, DateTimeOffset? lastModified, CancellationToken cancellationToken)
    {
        var config = Config;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(config.RequestTimeoutSeconds, 5, 600)));
        var requestToken = timeout.Token;
        var stage = "URL/DNS 检查";

        try
        {
            var fetched = await SendFollowingRedirectsAsync(
                url, etag, lastModified, config, requestToken, value => stage = value);
            using var response = fetched.Response;
            var finalUrl = fetched.FinalUrl;
            var responseEtag = response.Headers.ETag?.ToString();
            var responseLastModified = response.Content.Headers.LastModified;

            if (response.StatusCode == HttpStatusCode.NotModified)
                return new FeedFetchResult(null, [])
                {
                    NotModified = true,
                    ETag = responseEtag,
                    LastModified = responseLastModified
                };

            stage = "响应读取";
            await using var stream = await response.Content.ReadAsStreamAsync(requestToken);

            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                IgnoreComments = true,
                IgnoreWhitespace = true,
                CloseInput = false,
                Async = true
            };

            XDocument document;
            try
            {
                stage = "XML 解析";
                using var xmlReader = XmlReader.Create(stream, settings);
                document = await XDocument.LoadAsync(xmlReader, LoadOptions.None, requestToken);
            }
            catch (XmlException ex)
            {
                throw new InvalidOperationException($"解析 RSS/Atom 失败: {ex.Message}", ex);
            }

            stage = "条目解析";
            return RssParser.Parse(document.Root, finalUrl, config.MaxDescriptionLength, responseEtag, responseLastModified);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            throw new TimeoutException($"RSS 抓取在{stage}阶段超过 {Math.Clamp(config.RequestTimeoutSeconds, 5, 600)} 秒。", ex);
        }
    }

    private async Task<(HttpResponseMessage Response, string FinalUrl)> SendFollowingRedirectsAsync(
        string url, string? etag, DateTimeOffset? lastModified,
        KotoSubConfig config, CancellationToken cancellationToken, Action<string> setStage)
    {
        var currentUrl = url;
        for (var redirects = 0; redirects <= 5; redirects++)
        {
            setStage("URL/DNS 检查");
            var safety = await UrlSafetyGuard.CheckAsync(currentUrl, config.AllowPrivateUrls, cancellationToken);
            if (!safety.Allowed)
                throw new InvalidOperationException(safety.Reason);

            using var request = new HttpRequestMessage(HttpMethod.Get, currentUrl);
            if (!string.IsNullOrWhiteSpace(config.UserAgent))
                request.Headers.UserAgent.ParseAdd(config.UserAgent);
            request.Headers.Accept.ParseAdd("application/rss+xml, application/atom+xml, application/xml;q=0.9, text/xml;q=0.8, */*;q=0.5");
            if (EntityTagHeaderValue.TryParse(etag, out var parsedEtag))
                request.Headers.IfNoneMatch.Add(parsedEtag);
            if (lastModified is not null)
                request.Headers.IfModifiedSince = lastModified;

            setStage("HTTP 请求");
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308 &&
                response.Headers.Location is { } location)
            {
                response.Dispose();
                if (redirects == 5)
                    throw new InvalidOperationException("RSS 源重定向超过 5 次。");
                currentUrl = new Uri(new Uri(currentUrl), location).ToString();
                continue;
            }

            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotModified)
            {
                var error = new HttpRequestException(
                    $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}", null, response.StatusCode);
                response.Dispose();
                throw error;
            }

            return (response, currentUrl);
        }

        throw new InvalidOperationException("RSS 源重定向超过 5 次。");
    }
}
