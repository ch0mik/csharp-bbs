using Bbs.Core.Net;
using System.Net;
using System.Text.RegularExpressions;

namespace Bbs.Tenants.Content;

public sealed record CommodoreNewsItem(string Title, string Url);

public sealed record CommodoreNewsBlock(string Kind, string Text);

public sealed record CommodoreNewsArticle(string Title, string Url, IReadOnlyList<CommodoreNewsBlock> Blocks, IReadOnlyList<string> ImageUrls);

public sealed class CommodoreNewsService
{
    private const string BaseUrl = "https://www.commodore.net";
    private const string NewsUrl = BaseUrl + "/news";
    private const string SitemapUrl = BaseUrl + "/blog-posts-sitemap.xml";

    private static readonly Regex PostLinkWithTitleRegex = new(
        "<a[^>]+href\\s*=\\s*['\"](?<href>(?:https?:\\/\\/www\\.commodore\\.net)?\\/post\\/[^'\"#?]+)['\"][^>]*>(?<title>.*?)</a>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ImageTagRegex = new(
        "<img[^>]+(?:src|data-src|srcset|data-srcset)\\s*=\\s*['\"](?<src>[^'\"]+)['\"][^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex PostUrlRegex = new(
        "(?<url>(?:https?:\\/\\/www\\.commodore\\.net\\/post\\/[A-Za-z0-9_%\\-]+)|(?:https?:\\\\/\\\\/www\\\\.commodore\\\\.net\\\\/post\\\\/[A-Za-z0-9_%\\-]+)|(?:\\/post\\/[A-Za-z0-9_%\\-]+))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex H1Regex = new(
        "<h1[^>]*>(?<title>.*?)</h1>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ArticleRegex = new(
        "<article[^>]*>(?<body>.*?)</article>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex MainRegex = new(
        "<main[^>]*>(?<body>.*?)</main>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ContentBlocksRegex = new(
        "<(?<tag>h1|h2|h3|p|div|span)[^>]*>(?<body>.*?)</\\k<tag>>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex SitemapPostRegex = new(
        "<loc>\\s*(?<url>https?://(?:www\\.)?commodore\\.net/post/[^<]+)\\s*</loc>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CssDeclarationRegex = new(
        "\\b[a-z-]{2,}\\s*:\\s*[^;]{1,80};",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DirectImageUrlRegex = new(
        "(?<url>https?:\\\\?/\\\\?/(?:static\\.)?wixstatic\\.com/media/[^\"'\\s<>]+\\.(?:png|jpe?g|gif|webp)[^\"'\\s<>]*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IHttpService _http;

    public CommodoreNewsService(IHttpService? http = null)
    {
        _http = http ?? new HttpService();
    }

    public async Task<IReadOnlyList<CommodoreNewsItem>> GetLatestAsync(int maxItems = 120, CancellationToken cancellationToken = default)
    {
        var html = await _http.GetStringAsync(NewsUrl, cancellationToken: cancellationToken).ConfigureAwait(false);

        var items = new List<CommodoreNewsItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in PostLinkWithTitleRegex.Matches(html))
        {
            var href = NormalizePostUrl(match.Groups["href"].Value);
            var title = NormalizeTitle(match.Groups["title"].Value);

            if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            if (!seen.Add(href))
            {
                continue;
            }

            items.Add(new CommodoreNewsItem(title, href));
            if (items.Count >= maxItems)
            {
                break;
            }
        }

        foreach (Match match in PostUrlRegex.Matches(html))
        {
            var href = NormalizePostUrl(match.Groups["url"].Value);
            if (string.IsNullOrWhiteSpace(href) || !seen.Add(href))
            {
                continue;
            }

            items.Add(new CommodoreNewsItem(TitleFromUrl(href), href));
            if (items.Count >= maxItems)
            {
                break;
            }
        }
        // Fallback for lazy-loaded feeds: merge post URLs from sitemap.
        if (items.Count < maxItems)
        {
            var sitemapUrls = await TryGetPostUrlsFromSitemapAsync(cancellationToken).ConfigureAwait(false);
            foreach (var href in sitemapUrls)
            {
                if (string.IsNullOrWhiteSpace(href) || !seen.Add(href))
                {
                    continue;
                }

                items.Add(new CommodoreNewsItem(TitleFromUrl(href), href));
                if (items.Count >= maxItems)
                {
                    break;
                }
            }
        }

        for (var i = 0; i < items.Count; i++)
        {
            if (!LooksLikeSlugTitle(items[i].Title))
            {
                continue;
            }

            try
            {
                var article = await GetArticleAsync(items[i].Url, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(article.Title))
                {
                    items[i] = items[i] with { Title = article.Title };
                }
            }
            catch
            {
                // Keep slug title when fetching details fails.
            }
        }

        return items;
    }

    public async Task<CommodoreNewsArticle> GetArticleAsync(string url, CancellationToken cancellationToken = default)
    {
        var html = await _http.GetStringAsync(url, cancellationToken: cancellationToken).ConfigureAwait(false);

        var title = ExtractTitle(html);
        var contentHtml = ExtractArticleSection(html);
        var blocks = ExtractBlocks(contentHtml, title);

        var images = ExtractImageUrls(contentHtml, url);
        DebugLog($"Extract images from article section: url='{url}', count={images.Count}");
        if (images.Count == 0)
        {
            images = ExtractImageUrls(html, url);
            DebugLog($"Extract images from full HTML fallback: url='{url}', count={images.Count}");
        }

        if (images.Count > 0)
        {
            var preview = string.Join(" | ", images.Take(3));
            DebugLog($"Image URL preview (max 3): {preview}");
        }

        return new CommodoreNewsArticle(title, url, blocks, images);
    }

    private static string ExtractArticleSection(string html)
    {
        string section;

        var article = ArticleRegex.Match(html);
        if (article.Success)
        {
            section = article.Groups["body"].Value;
        }
        else
        {
            var main = MainRegex.Match(html);
            section = main.Success ? main.Groups["body"].Value : html;
        }

        return CutBeforeFooterTag(section);
    }

    private static string CutBeforeFooterTag(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var footerIdx = html.IndexOf("<footer", StringComparison.OrdinalIgnoreCase);
        if (footerIdx >= 0)
        {
            return html[..footerIdx];
        }

        return html;
    }

    private static IReadOnlyList<CommodoreNewsBlock> ExtractBlocks(string html, string title)
    {
        var blocks = new List<CommodoreNewsBlock>();
        var seenTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in ContentBlocksRegex.Matches(html))
        {
            var tag = match.Groups["tag"].Value.ToLowerInvariant();
            var raw = match.Groups["body"].Value;
            var text = CleanBlockText(raw);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (IsFooterText(text))
            {
                break;
            }

            if (ShouldSkipText(text))
            {
                continue;
            }

            if (tag == "div" && !LooksLikeReadableDiv(text))
            {
                continue;
            }

            if (tag == "span" && !LooksLikeReadableSpan(text))
            {
                continue;
            }

            if (!seenTexts.Add(text))
            {
                continue;
            }

            blocks.Add(new CommodoreNewsBlock(tag, text));
            if (blocks.Count >= 256)
            {
                break;
            }
        }

        if (blocks.Count == 0)
        {
            var fallback = CleanBlockText(TextRender.SanitizeHtmlToText(html));
            if (!string.IsNullOrWhiteSpace(fallback))
            {
                blocks.Add(new CommodoreNewsBlock("h1", title));
                blocks.Add(new CommodoreNewsBlock("p", fallback));
            }
        }

        return blocks;
    }

    private static IReadOnlyList<string> ExtractImageUrls(string html, string articleUrl)
    {
        if (string.IsNullOrWhiteSpace(html) || string.IsNullOrWhiteSpace(articleUrl))
        {
            return Array.Empty<string>();
        }

        if (!Uri.TryCreate(articleUrl, UriKind.Absolute, out var baseUri))
        {
            baseUri = new Uri(BaseUrl);
        }

        var urls = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in ImageTagRegex.Matches(html))
        {
            var raw = match.Groups["src"].Value;
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var candidate = WebUtility.HtmlDecode(raw.Trim()).Replace("\\/", "/", StringComparison.Ordinal);
            if (candidate.Contains(',', StringComparison.Ordinal))
            {
                candidate = candidate.Split(',', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? candidate;
            }

            if (candidate.Contains(' ', StringComparison.Ordinal))
            {
                candidate = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? candidate;
            }

            var normalized = NormalizeImageUrl(candidate, baseUri);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            if (seen.Add(normalized))
            {
                urls.Add(normalized);
            }
        }


        if (urls.Count == 0)
        {
            foreach (Match match in DirectImageUrlRegex.Matches(html))
            {
                var raw = match.Groups["url"].Value;
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                var candidate = WebUtility.HtmlDecode(raw.Trim()).Replace("\\/", "/", StringComparison.Ordinal);
                var normalized = NormalizeImageUrl(candidate, baseUri);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                if (seen.Add(normalized))
                {
                    urls.Add(normalized);
                }
            }
        }

        return urls;
    }

    private static bool LooksLikeReadableSpan(string text)
    {
        if (IsLikelyCssOrJs(text))
        {
            return false;
        }

        if (LooksLikeUsefulShortText(text) || ContainsUrl(text))
        {
            return true;
        }

        var wordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return text.Length >= 18 && wordCount >= 4;
    }

    private static bool LooksLikeReadableDiv(string text)
    {
        if (IsLikelyCssOrJs(text))
        {
            return false;
        }

        if (LooksLikeUsefulShortText(text) || ContainsUrl(text))
        {
            return true;
        }

        var wordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return text.Length >= 25 && wordCount >= 5;
    }

    private static bool LooksLikeUsefulShortText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.StartsWith("Find ", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("Music:", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("YouTube:", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("Facebook:", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("Instagram:", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("X:", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("Bluesky:", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("Links:", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("Contact:", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("About ", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsUrl(string text)
        => text.Contains("http://", StringComparison.OrdinalIgnoreCase)
            || text.Contains("https://", StringComparison.OrdinalIgnoreCase)
            || text.Contains("www.", StringComparison.OrdinalIgnoreCase);

    private static bool IsFooterText(string text)
    {
        return text.Contains("Only essential cookies here", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Privacy & Terms", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Privacy & terms", StringComparison.OrdinalIgnoreCase)
            || text.Equals("bottom of page", StringComparison.OrdinalIgnoreCase)
            || text.Contains("focus-friendly computing the Commodore way", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldSkipText(string text)
    {
        return text.Equals("Bring a Commodore Home", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Recent Posts", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Comments", StringComparison.OrdinalIgnoreCase)
            || text.Equals("All Posts", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Team Commodore", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("Image:", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Home About Team Store", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Press Play Blog Contact Us", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Downloads Log In All Posts", StringComparison.OrdinalIgnoreCase)
            || IsFooterText(text)
            || IsLikelyCssOrJs(text);
    }

    private static bool IsLikelyCssOrJs(string text)
    {
        if (text.Contains("var(--", StringComparison.OrdinalIgnoreCase)
            || text.Contains(":root", StringComparison.OrdinalIgnoreCase)
            || text.Contains("display:", StringComparison.OrdinalIgnoreCase)
            || text.Contains("font-", StringComparison.OrdinalIgnoreCase)
            || text.Contains("justify-content", StringComparison.OrdinalIgnoreCase)
            || text.Contains("box-sizing", StringComparison.OrdinalIgnoreCase)
            || text.Contains("rgb(", StringComparison.OrdinalIgnoreCase)
            || text.Contains(".__", StringComparison.Ordinal)
            || text.Contains("._", StringComparison.Ordinal))
        {
            return true;
        }

        var cssDecls = CssDeclarationRegex.Matches(text).Count;
        return cssDecls >= 2;
    }

    private static string CleanBlockText(string text)
    {
        var value = TextRender.SanitizeHtmlToText(WebUtility.HtmlDecode(text ?? string.Empty));
        value = Regex.Replace(value, "Kickstarter pre-launch page\\s*-\\s*Bring a Commodore Home", "", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, "Bring a Commodore Home", "", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, "\\s+", " ").Trim();
        return value;
    }


    private async Task<IReadOnlyList<string>> TryGetPostUrlsFromSitemapAsync(CancellationToken cancellationToken)
    {
        try
        {
            var xml = await _http.GetStringAsync(SitemapUrl, cancellationToken: cancellationToken).ConfigureAwait(false);
            var urls = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match match in SitemapPostRegex.Matches(xml))
            {
                var normalized = NormalizePostUrl(match.Groups["url"].Value);
                if (string.IsNullOrWhiteSpace(normalized) || !seen.Add(normalized))
                {
                    continue;
                }

                urls.Add(normalized);
            }

            return urls;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
    private static string NormalizePostUrl(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var url = raw.Trim().Replace("\\/", "/", StringComparison.Ordinal);

        if (url.StartsWith("/post/", StringComparison.OrdinalIgnoreCase))
        {
            url = BaseUrl + url;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
        {
            return absolute.GetLeftPart(UriPartial.Path);
        }

        return string.Empty;
    }

    private static string? NormalizeImageUrl(string raw, Uri baseUri)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim();
        if (value.StartsWith("//", StringComparison.Ordinal))
        {
            value = baseUri.Scheme + ":" + value;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var absolute))
        {
            if (!Uri.TryCreate(baseUri, value, out absolute))
            {
                return null;
            }
        }

        if (absolute.Scheme != Uri.UriSchemeHttp && absolute.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        return NormalizeWixMediaUrl(absolute.AbsoluteUri);
    }

    private static string NormalizeWixMediaUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        // Wix often serves transformed URLs like:
        // .../image.jpg/v1/fill/w_147,h_97,al_c,q_80,enc_avif,quality_auto/...
        // For our converter we want the original image URL ending with extension.
        if (!url.Contains("wixstatic.com/media/", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        var markerPos = url.IndexOf("/v1/", StringComparison.OrdinalIgnoreCase);
        if (markerPos < 0)
        {
            return url;
        }

        var prefix = url[..markerPos];
        var extPos = LastImageExtensionEnd(prefix);
        if (extPos <= 0)
        {
            return url;
        }

        return prefix[..extPos];
    }

    private static int LastImageExtensionEnd(string urlPart)
    {
        ReadOnlySpan<string> exts = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
        var best = -1;
        foreach (var ext in exts)
        {
            var idx = urlPart.LastIndexOf(ext, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                continue;
            }

            var end = idx + ext.Length;
            if (end > best)
            {
                best = end;
            }
        }

        return best;
    }

    private static string NormalizeTitle(string raw)
    {
        var title = CleanBlockText(raw);
        return ShouldSkipText(title) ? string.Empty : title;
    }

    private static string TitleFromUrl(string url)
    {
        var slug = url.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "news";
        slug = slug.Replace('-', ' ');
        return CultureInfoInvariantTitle(slug);
    }

    private static string CultureInfoInvariantTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Commodore News";
        }

        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < words.Length; i++)
        {
            var w = words[i];
            words[i] = w.Length == 1
                ? w.ToUpperInvariant()
                : char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant();
        }

        return string.Join(' ', words);
    }

    private static bool LooksLikeSlugTitle(string title)
    {
        return !string.IsNullOrWhiteSpace(title)
            && title.All(ch => char.IsLetterOrDigit(ch) || ch == ' ');
    }

    private static string ExtractTitle(string html)
    {
        var h1 = H1Regex.Match(html);
        if (h1.Success)
        {
            var raw = h1.Groups["title"].Value;
            var title = TextRender.SanitizeHtmlToText(raw);
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }
        }

        var all = TextRender.SanitizeHtmlToText(html);
        var fallback = TextRender.TrimTo(all, 64);
        return string.IsNullOrWhiteSpace(fallback) ? "Commodore News" : fallback;
    }

    private static void DebugLog(string message)
    {
        Console.WriteLine($"[{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}][CommodoreNewsService] {message}");
    }
}







