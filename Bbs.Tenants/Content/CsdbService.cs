using System.Text.RegularExpressions;
using System.Xml;
using Bbs.Core;
using Bbs.Core.Content;
using Bbs.Core.Net;

namespace Bbs.Tenants.Content;

public class CsdbService : ICsdbService
{
    private const string RssLatestReleases = "https://csdb.dk/rss/latestreleases.php";
    private const string RssLatestAdditions = "https://csdb.dk/rss/latestadditions.php?type=release";
    private const string SearchUrlTemplate = "https://csdb.dk/search/?seinsel=releases&all=1&search=";
    private const string OtherPlatformType = "Other Platform C64 Tool";

    private readonly IHttpService _http;

    public CsdbService(IHttpService? http = null)
    {
        _http = http ?? new HttpService();
    }

    public async Task<IReadOnlyList<CsdbReleaseItem>> GetLatestReleasesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var rssContent = await _http.GetStringAsync(RssLatestReleases, cancellationToken: cancellationToken).ConfigureAwait(false);
            var feeds = ParseRss(rssContent);
            return ExtractReleases(feeds);
        }
        catch (Exception ex)
        {
            throw new BbsIOException($"Failed to fetch latest CSDb releases: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<CsdbReleaseItem>> SearchReleasesAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<CsdbReleaseItem>();
        }

        try
        {
            var url = SearchUrlTemplate + Uri.EscapeDataString(query);
            var htmlContent = await _http.GetStringAsync(url, cancellationToken: cancellationToken).ConfigureAwait(false);
            return ExtractSearchResults(htmlContent);
        }
        catch (Exception ex)
        {
            throw new BbsIOException($"Failed to search CSDb releases: {ex.Message}");
        }
    }

    public async Task<DownloadPayload?> DownloadReleaseAsync(CsdbReleaseItem item, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(item.DownloadLink))
        {
            // Try to find download link from release page
            try
            {
                var htmlContent = await _http.GetStringAsync(item.ReleaseUri, cancellationToken: cancellationToken).ConfigureAwait(false);
                var downloadUrl = FindDownloadLink(htmlContent);
                if (downloadUrl == null)
                {
                    return null;
                }

                var content = await _http.GetBytesAsync(downloadUrl, cancellationToken: cancellationToken).ConfigureAwait(false);
                var fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
                return new DownloadPayload(fileName, content, downloadUrl);
            }
            catch (Exception ex)
            {
                throw new BbsIOException($"Failed to download release: {ex.Message}");
            }
        }

        try
        {
            var content = await _http.GetBytesAsync(item.DownloadLink, cancellationToken: cancellationToken).ConfigureAwait(false);
            var fileName = Path.GetFileName(new Uri(item.DownloadLink).LocalPath);
            return new DownloadPayload(fileName, content, item.DownloadLink);
        }
        catch (Exception ex)
        {
            throw new BbsIOException($"Failed to download from {item.DownloadLink}: {ex.Message}");
        }
    }

    private List<RssFeedItem> ParseRss(string rssContent)
    {
        var result = new List<RssFeedItem>();

        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(rssContent);

            var items = doc.GetElementsByTagName("item");
            foreach (XmlNode item in items)
            {
                var title = item.SelectSingleNode("title")?.InnerText ?? "";
                // Remove " by ..." part from title
                title = Regex.Replace(title, @"\s+by\s+.*$", "", RegexOptions.IgnoreCase);

                var description = item.SelectSingleNode("description")?.InnerText ?? "";
                var link = item.SelectSingleNode("link")?.InnerText ?? "";
                var pubDate = item.SelectSingleNode("pubDate")?.InnerText ?? "";

                if (DateTime.TryParse(pubDate, out var publishedDate))
                {
                    result.Add(new RssFeedItem(publishedDate, title, description, link));
                }
                else
                {
                    result.Add(new RssFeedItem(DateTime.UtcNow, title, description, link));
                }
            }
        }
        catch (Exception ex)
        {
            throw new BbsIOException($"Failed to parse RSS feed: {ex.Message}");
        }

        return result;
    }

    private List<CsdbReleaseItem> ExtractReleases(List<RssFeedItem> feeds)
    {
        var result = new List<CsdbReleaseItem>();
        var downloadLinkPattern = new Regex(@"href=""([^""]*?\.(p00|prg|zip|t64|d64|d71|d81|d82|d64\.gz|d71\.gz|d81\.gz|d82\.gz|t64\.gz))""", RegexOptions.IgnoreCase);
        var hasDownloadPattern = new Regex(@"=\s*""([^""]*?\.(p00|prg|zip|t64|d64|d71|d81|d82|d64\.gz|d71\.gz|d81\.gz|d82\.gz|t64\.gz))""", RegexOptions.IgnoreCase);

        foreach (var feed in feeds)
        {
            // Check if description contains downloadable file
            if (!hasDownloadPattern.IsMatch(feed.Description))
            {
                continue;
            }

            var id = ExtractId(feed.Uri);
            var releasedBy = ExtractReleasedBy(feed.Description);
            var type = ExtractType(feed.Description);

            // Skip "Other Platform" releases
            if (type.Equals(OtherPlatformType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var downloadMatch = downloadLinkPattern.Match(feed.Description);
            var downloadLink = downloadMatch.Success ? downloadMatch.Groups[1].Value : null;

            result.Add(new CsdbReleaseItem(
                Id: id,
                Title: feed.Title,
                Type: type,
                ReleaseUri: feed.Uri,
                ReleasedBy: releasedBy,
                PublishedAt: feed.PublishedDate,
                DownloadLink: downloadLink));
        }

        return result;
    }

    private List<CsdbReleaseItem> ExtractSearchResults(string htmlContent)
    {
        var result = new List<CsdbReleaseItem>();

        // Pattern to extract release links from search results
        var releasePattern = new Regex(@"href=""([^""]*?release/\?id=([^&""]+)[^""]*?)""[^>]*>([^<]+)<", RegexOptions.IgnoreCase);
        var downloadPattern = new Regex(@"href=""([^""]*?\.(p00|prg|zip|t64|d64|d71|d81|d82|d64\.gz|d71\.gz|d81\.gz|d82\.gz|t64\.gz))""", RegexOptions.IgnoreCase);

        var matches = releasePattern.Matches(htmlContent);
        foreach (Match match in matches)
        {
            var uri = "https://csdb.dk" + match.Groups[1].Value;
            var id = match.Groups[2].Value.Trim();
            var title = TextRender.SanitizeHtmlToText(match.Groups[3].Value.Trim());

            // Try to find download link
            var downloadMatch = downloadPattern.Match(htmlContent);
            var downloadLink = downloadMatch.Success ? downloadMatch.Groups[1].Value : null;

            result.Add(new CsdbReleaseItem(
                Id: id,
                Title: title,
                Type: "",
                ReleaseUri: uri,
                ReleasedBy: "",
                PublishedAt: null,
                DownloadLink: downloadLink));
        }

        return result;
    }

    private string ExtractId(string uri)
    {
        var match = Regex.Match(uri, @"id=([0-9a-zA-Z_\-]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : "";
    }

    private string ExtractReleasedBy(string description)
    {
        var pattern = new Regex(@"Released by:\s*<a [^>]*>([^<]*)<", RegexOptions.IgnoreCase);
        var match = pattern.Match(description);
        return match.Success ? TextRender.SanitizeHtmlToText(match.Groups[1].Value.Trim()) : "";
    }

    private string ExtractType(string description)
    {
        var pattern = new Regex(@"Type:\s*[^>]*>([^<]*)<", RegexOptions.IgnoreCase);
        var match = pattern.Match(description);
        return match.Success ? TextRender.SanitizeHtmlToText(match.Groups[1].Value.Trim()) : "";
    }

    private string? FindDownloadLink(string htmlContent)
    {
        var pattern = new Regex(@"href=""([^""]*?\.(p00|prg|zip|t64|d64|d71|d81|d82|d64\.gz|d71\.gz|d81\.gz|d82\.gz|t64\.gz))""", RegexOptions.IgnoreCase);
        var match = pattern.Match(htmlContent);
        if (match.Success)
        {
            var url = match.Groups[1].Value;
            // Make relative URL absolute
            if (url.StartsWith("/"))
            {
                url = "https://csdb.dk" + url;
            }
            else if (!url.StartsWith("http"))
            {
                url = "https://csdb.dk/" + url;
            }

            return url;
        }

        return null;
    }

    private record RssFeedItem(DateTime PublishedDate, string Title, string Description, string Uri);
}
