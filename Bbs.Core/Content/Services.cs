using Bbs.Core.Net;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Bbs.Core.Content;

public sealed class RssService : IRssService
{
    private readonly IHttpService _http;

    public RssService(IHttpService? http = null)
    {
        _http = http ?? new HttpService();
    }

    public async Task<IReadOnlyList<RssEntry>> ReadFeedAsync(string url, CancellationToken cancellationToken = default)
    {
        var xml = await _http.GetStringAsync(url, cancellationToken: cancellationToken).ConfigureAwait(false);
        var doc = XDocument.Parse(xml);

        var items = doc.Descendants().Where(e => e.Name.LocalName is "item" or "entry");
        var results = new List<RssEntry>();

        foreach (var item in items)
        {
            var title = GetElementValue(item, "title") ?? string.Empty;
            var description = GetElementValue(item, "description") ?? GetElementValue(item, "summary") ?? string.Empty;
            var link =
                GetElementValue(item, "link") ??
                item.Elements().FirstOrDefault(e => e.Name.LocalName == "link")?.Attribute("href")?.Value ??
                GetElementValue(item, "guid") ??
                string.Empty;

            DateTimeOffset? published = null;
            var dateRaw = GetElementValue(item, "pubDate") ?? GetElementValue(item, "published") ?? GetElementValue(item, "updated");
            if (DateTimeOffset.TryParse(dateRaw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            {
                published = parsed;
            }

            results.Add(new RssEntry(title, description, link, published));
        }

        return results;
    }

    private static string? GetElementValue(XElement parent, string localName)
    {
        return parent.Elements().FirstOrDefault(e => e.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))?.Value;
    }
}

public sealed class WordpressService : IWordpressService
{
    private readonly IHttpService _http;

    public WordpressService(IHttpService? http = null)
    {
        _http = http ?? new HttpService();
    }

    public async Task<IReadOnlyList<WordpressPostSummary>> GetPostsAsync(
        string domain,
        int page,
        int pageSize,
        string? categoriesId = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        var safeDomain = NormalizeDomain(domain);
        var url = $"{safeDomain}/wp-json/wp/v2/posts?context=view&page={Math.Max(1, page)}&per_page={Math.Max(1, pageSize)}";
        if (!string.IsNullOrWhiteSpace(categoriesId))
        {
            url += $"&categories={Uri.EscapeDataString(categoriesId.Trim())}";
        }

        var json = await _http.GetStringAsync(url, userAgent, cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);

        var results = new List<WordpressPostSummary>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            results.Add(new WordpressPostSummary(
                item.TryGetProperty("id", out var id) ? id.GetInt64() : 0,
                item.TryGetProperty("title", out var titleObj) && titleObj.TryGetProperty("rendered", out var title) ? title.GetString() ?? string.Empty : string.Empty,
                item.TryGetProperty("excerpt", out var excerptObj) && excerptObj.TryGetProperty("rendered", out var excerpt) ? excerpt.GetString() ?? string.Empty : string.Empty,
                item.TryGetProperty("date", out var date) && DateTimeOffset.TryParse(date.GetString(), out var parsedDate) ? parsedDate : null,
                item.TryGetProperty("author", out var author) ? author.GetInt64() : null));
        }

        return results;
    }

    public async Task<WordpressPostDetails> GetPostAsync(string domain, long postId, string? userAgent = null, CancellationToken cancellationToken = default)
    {
        var safeDomain = NormalizeDomain(domain);
        var url = $"{safeDomain}/wp-json/wp/v2/posts/{postId}?context=view";
        var json = await _http.GetStringAsync(url, userAgent, cancellationToken).ConfigureAwait(false);

        using var doc = JsonDocument.Parse(json);
        var item = doc.RootElement;

        return new WordpressPostDetails(
            item.TryGetProperty("id", out var id) ? id.GetInt64() : postId,
            item.TryGetProperty("title", out var titleObj) && titleObj.TryGetProperty("rendered", out var title) ? title.GetString() ?? string.Empty : string.Empty,
            item.TryGetProperty("content", out var contentObj) && contentObj.TryGetProperty("rendered", out var content) ? content.GetString() ?? string.Empty : string.Empty,
            item.TryGetProperty("excerpt", out var excerptObj) && excerptObj.TryGetProperty("rendered", out var excerpt) ? excerpt.GetString() ?? string.Empty : string.Empty,
            item.TryGetProperty("date", out var date) && DateTimeOffset.TryParse(date.GetString(), out var parsedDate) ? parsedDate : null,
            item.TryGetProperty("author", out var author) ? author.GetInt64() : null);
    }

    public async Task<string?> GetAuthorNameAsync(string domain, long authorId, string? userAgent = null, CancellationToken cancellationToken = default)
    {
        if (authorId <= 0)
        {
            return null;
        }

        var safeDomain = NormalizeDomain(domain);
        var url = $"{safeDomain}/wp-json/wp/v2/users/{authorId}";
        var json = await _http.GetStringAsync(url, userAgent, cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("name", out var name) ? name.GetString() : null;
    }

    private static string NormalizeDomain(string domain)
    {
        var value = (domain ?? string.Empty).Trim();
        if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            value = "https://" + value;
        }

        return value.TrimEnd('/');
    }
}

public sealed class WikipediaService : IWikipediaService
{
    private readonly IHttpService _http;

    public WikipediaService(IHttpService? http = null)
    {
        _http = http ?? new HttpService();
    }

    public async Task<IReadOnlyList<WikipediaSearchItem>> SearchAsync(string language, string query, int limit = 20, CancellationToken cancellationToken = default)
    {
        var lang = NormalizeLang(language);
        var safeQuery = Uri.EscapeDataString(query ?? string.Empty);
        var url = $"https://{lang}.wikipedia.org/w/api.php?format=json&action=query&list=search&srsearch={safeQuery}&srlimit={Math.Clamp(limit, 1, 50)}";
        var json = await _http.GetStringAsync(url, cancellationToken: cancellationToken).ConfigureAwait(false);

        using var doc = JsonDocument.Parse(json);
        var list = new List<WikipediaSearchItem>();
        if (!doc.RootElement.TryGetProperty("query", out var queryObj) || !queryObj.TryGetProperty("search", out var searchArray))
        {
            return list;
        }

        foreach (var item in searchArray.EnumerateArray())
        {
            list.Add(new WikipediaSearchItem(
                lang,
                item.TryGetProperty("pageid", out var pageid) ? pageid.GetInt64() : -1,
                item.TryGetProperty("title", out var title) ? title.GetString() ?? string.Empty : string.Empty,
                item.TryGetProperty("snippet", out var snippet) ? snippet.GetString() ?? string.Empty : string.Empty,
                item.TryGetProperty("wordcount", out var wc) ? wc.GetInt64() : -1,
                item.TryGetProperty("size", out var size) ? size.GetInt64() : -1,
                item.TryGetProperty("timestamp", out var ts) ? ts.GetString() ?? string.Empty : string.Empty));
        }

        return list;
    }

    public async Task<WikipediaSearchItem?> PickRandomAsync(string language, CancellationToken cancellationToken = default)
    {
        var lang = NormalizeLang(language);
        var url = $"https://{lang}.wikipedia.org/w/api.php?action=query&format=json&list=random&rnnamespace=0";
        var json = await _http.GetStringAsync(url, cancellationToken: cancellationToken).ConfigureAwait(false);

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("query", out var queryObj) || !queryObj.TryGetProperty("random", out var randomArray))
        {
            return null;
        }

        var first = randomArray.EnumerateArray().FirstOrDefault();
        if (first.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        return new WikipediaSearchItem(
            lang,
            first.TryGetProperty("id", out var id) ? id.GetInt64() : -1,
            first.TryGetProperty("title", out var title) ? title.GetString() ?? string.Empty : string.Empty,
            string.Empty,
            -1,
            -1,
            string.Empty);
    }

    public async Task<string> GetArticleHtmlAsync(string language, long pageId, CancellationToken cancellationToken = default)
    {
        var lang = NormalizeLang(language);
        var url = $"https://{lang}.wikipedia.org/w/api.php?format=json&formatversion=2&action=parse&prop=text&pageid={pageId}&mobileformat=true&disableeditsection=true&disabletoc=true";
        var json = await _http.GetStringAsync(url, cancellationToken: cancellationToken).ConfigureAwait(false);

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("parse", out var parseObj)
            || !parseObj.TryGetProperty("text", out var textObj))
        {
            return string.Empty;
        }

        if (textObj.ValueKind == JsonValueKind.String)
        {
            return textObj.GetString() ?? string.Empty;
        }

        if (textObj.ValueKind == JsonValueKind.Object
            && textObj.TryGetProperty("*", out var htmlObj))
        {
            return htmlObj.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    public async Task<IReadOnlyList<string>> GetArticleImageUrlsAsync(string language, long pageId, int limit = 24, CancellationToken cancellationToken = default)
    {
        var lang = NormalizeLang(language);
        var max = Math.Clamp(limit, 1, 100);
        var orderedTitles = new List<string>(max);
        string? imContinue = null;

        while (orderedTitles.Count < max)
        {
            var url = $"https://{lang}.wikipedia.org/w/api.php?format=json&formatversion=2&action=query&prop=images&pageids={pageId}&imlimit=50";
            if (!string.IsNullOrWhiteSpace(imContinue))
            {
                url += $"&imcontinue={Uri.EscapeDataString(imContinue)}";
            }

            var json = await _http.GetStringAsync(url, cancellationToken: cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("query", out var queryObj)
                || !queryObj.TryGetProperty("pages", out var pagesObj)
                || pagesObj.ValueKind != JsonValueKind.Array
                || pagesObj.GetArrayLength() == 0)
            {
                break;
            }

            var page = pagesObj[0];
            if (page.TryGetProperty("images", out var images) && images.ValueKind == JsonValueKind.Array)
            {
                foreach (var img in images.EnumerateArray())
                {
                    if (!img.TryGetProperty("title", out var titleEl))
                    {
                        continue;
                    }

                    var title = titleEl.GetString();
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        continue;
                    }

                    orderedTitles.Add(title);
                    if (orderedTitles.Count >= max)
                    {
                        break;
                    }
                }
            }

            if (!doc.RootElement.TryGetProperty("continue", out var contObj)
                || !contObj.TryGetProperty("imcontinue", out var imcontEl))
            {
                break;
            }

            imContinue = imcontEl.GetString();
            if (string.IsNullOrWhiteSpace(imContinue))
            {
                break;
            }
        }

        if (orderedTitles.Count == 0)
        {
            return Array.Empty<string>();
        }

        var distinctTitles = orderedTitles.Distinct(StringComparer.OrdinalIgnoreCase).Take(max).ToArray();
        var imageByTitle = new Dictionary<string, (string Url, int Width, int Height)>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < distinctTitles.Length; i += 50)
        {
            var chunk = distinctTitles.Skip(i).Take(50).ToArray();
            var titlesArg = string.Join("|", chunk.Select(Uri.EscapeDataString));
            var iiUrl = $"https://{lang}.wikipedia.org/w/api.php?format=json&formatversion=2&action=query&prop=imageinfo&iiprop=url|size&titles={titlesArg}";
            var iiJson = await _http.GetStringAsync(iiUrl, cancellationToken: cancellationToken).ConfigureAwait(false);
            using var iiDoc = JsonDocument.Parse(iiJson);

            if (!iiDoc.RootElement.TryGetProperty("query", out var q)
                || !q.TryGetProperty("pages", out var pages)
                || pages.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var p in pages.EnumerateArray())
            {
                if (!p.TryGetProperty("title", out var titleEl))
                {
                    continue;
                }

                var title = titleEl.GetString();
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                if (!p.TryGetProperty("imageinfo", out var infos)
                    || infos.ValueKind != JsonValueKind.Array
                    || infos.GetArrayLength() == 0)
                {
                    continue;
                }

                var info = infos[0];
                var imgUrl = info.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : null;
                var width = info.TryGetProperty("width", out var wEl) ? wEl.GetInt32() : 0;
                var height = info.TryGetProperty("height", out var hEl) ? hEl.GetInt32() : 0;
                if (string.IsNullOrWhiteSpace(imgUrl))
                {
                    continue;
                }

                imageByTitle[title] = (imgUrl, width, height);
            }
        }

        var result = new List<string>(max);
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var title in orderedTitles)
        {
            if (!imageByTitle.TryGetValue(title, out var img))
            {
                continue;
            }

            // Wiki-specific minimum: skip tiny icon-like assets.
            if (img.Width > 0 && img.Width < 320)
            {
                continue;
            }

            if (img.Height > 0 && img.Height < 200)
            {
                continue;
            }

            if (seenUrls.Add(img.Url))
            {
                result.Add(img.Url);
                if (result.Count >= max)
                {
                    break;
                }
            }
        }

        return result;
    }

    private static string NormalizeLang(string language)
    {
        var clean = new string((language ?? "en").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return string.IsNullOrWhiteSpace(clean) ? "en" : clean;
    }
}

public sealed class CsdbService : ICsdbService
{
    private const string LatestUrl = "https://csdb.dk/rss/latestreleases.php";

    private readonly IRssService _rss;
    private readonly IHttpService _http;

    public CsdbService(IRssService? rss = null, IHttpService? http = null)
    {
        _rss = rss ?? new RssService(http);
        _http = http ?? new HttpService();
    }

    public async Task<IReadOnlyList<CsdbReleaseItem>> GetLatestReleasesAsync(CancellationToken cancellationToken = default)
    {
        var feed = await _rss.ReadFeedAsync(LatestUrl, cancellationToken).ConfigureAwait(false);
        return feed.Select(MapFromRss).ToArray();
    }

    public async Task<IReadOnlyList<CsdbReleaseItem>> SearchReleasesAsync(string query, CancellationToken cancellationToken = default)
    {
        var safe = Uri.EscapeDataString(query ?? string.Empty);
        var url = $"https://csdb.dk/search/?seinsel=releases&all=1&search={safe}";
        var html = await _http.GetStringAsync(url, cancellationToken: cancellationToken).ConfigureAwait(false);

        var list = new List<CsdbReleaseItem>();
        var regex = new Regex("<a href=\"(?<href>/release/\\?id=[0-9A-Za-z_-]+)\">(?<title>[^<]+)</a>\\s*\\((?<type>[^\\)]+)\\)", RegexOptions.IgnoreCase);
        foreach (Match m in regex.Matches(html))
        {
            var href = m.Groups["href"].Value;
            var id = Regex.Match(href, "id=([0-9A-Za-z_-]+)", RegexOptions.IgnoreCase).Groups[1].Value;
            var releaseUri = "https://csdb.dk" + href;
            list.Add(new CsdbReleaseItem(
                id,
                HtmlDecode(m.Groups["title"].Value),
                HtmlDecode(m.Groups["type"].Value),
                releaseUri,
                string.Empty,
                null,
                null));
        }

        return list;
    }

    public async Task<DownloadPayload?> DownloadReleaseAsync(CsdbReleaseItem item, CancellationToken cancellationToken = default)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.ReleaseUri))
        {
            return null;
        }

        var releasePage = await _http.GetStringAsync(item.ReleaseUri, cancellationToken: cancellationToken).ConfigureAwait(false);
        var links = ParseDownloadLinks(releasePage);
        if (links.Count == 0)
        {
            return null;
        }

        var selected = SelectBestLink(links);
        if (selected is null)
        {
            return null;
        }

        var chosen = selected.Value;
        var bytes = await _http.GetBytesAsync(chosen.Url, cancellationToken: cancellationToken).ConfigureAwait(false);
        return C64DownloadPayloadNormalizer.Normalize(chosen.FileName, bytes, chosen.Url);
    }

    private static CsdbReleaseItem MapFromRss(RssEntry e)
    {
        var id = Regex.Match(e.Link ?? string.Empty, "id=([0-9A-Za-z_-]+)", RegexOptions.IgnoreCase).Groups[1].Value;
        var title = Regex.Replace(e.Title ?? string.Empty, "\\s+by\\s+.*$", string.Empty, RegexOptions.IgnoreCase);
        return new CsdbReleaseItem(id, HtmlDecode(title), string.Empty, e.Link ?? string.Empty, string.Empty, e.PublishedAt, null);
    }

    private static List<(string Url, string FileName)> ParseDownloadLinks(string html)
    {
        var result = new List<(string Url, string FileName)>();
        var regex = new Regex("<a href=\"(?<href>download\\.php\\?id=[^\"]+)\">(?<name>[^<]+)</a>", RegexOptions.IgnoreCase);
        foreach (Match m in regex.Matches(html ?? string.Empty))
        {
            var href = m.Groups["href"].Value;
            var name = HtmlDecode(m.Groups["name"].Value).Trim();
            var full = href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? href
                : "https://csdb.dk/release/" + href;
            result.Add((full, string.IsNullOrWhiteSpace(name) ? "download.bin" : name));
        }

        return result;
    }

    private static (string Url, string FileName)? SelectBestLink(List<(string Url, string FileName)> links)
    {
        if (links.Count == 0)
        {
            return null;
        }

        var preferred = new[] { ".prg", ".p00", ".t64", ".d64", ".zip", ".d71", ".d81", ".d82" };
        foreach (var ext in preferred)
        {
            var hit = links.FirstOrDefault(l => l.FileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(hit.Url))
            {
                return hit;
            }
        }

        return links[0];
    }

    private static string HtmlDecode(string value)
    {
        return System.Net.WebUtility.HtmlDecode(value ?? string.Empty);
    }
}

public sealed class PetsciiGalleryService : IPetsciiGalleryService
{
    private static readonly ConcurrentDictionary<string, ImageCacheEntry> ImageCache = new(StringComparer.OrdinalIgnoreCase);

    private sealed record ImageCacheEntry(DateTime LastWriteUtc, long Length, byte[] Data);
    public Task<IReadOnlyList<string>> ListAuthorsAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(rootPath))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        var authors = Directory.EnumerateDirectories(rootPath)
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult<IReadOnlyList<string>>(authors);
    }

    public Task<IReadOnlyList<string>> ListDrawingsAsync(string authorPath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(authorPath))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        var files = Directory.EnumerateFiles(authorPath, "*", SearchOption.AllDirectories)
            .Where(path =>
            {
                var fileName = Path.GetFileName(path);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    return false;
                }

                // Hide metadata/docs from the viewer, keep binary/extension-less artwork.
                return !fileName.Equals("README", StringComparison.OrdinalIgnoreCase)
                    && !fileName.Equals("README.md", StringComparison.OrdinalIgnoreCase)
                    && !fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                    && !fileName.EndsWith(".bas", StringComparison.OrdinalIgnoreCase)
                    && !fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                    && !fileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
                    && !fileName.EndsWith("_screen.seq", StringComparison.OrdinalIgnoreCase)
                    && !fileName.EndsWith("_color.seq", StringComparison.OrdinalIgnoreCase)
                    && !fileName.EndsWith("_bgcolor.seq", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult<IReadOnlyList<string>>(files);
    }

    public async Task<byte[]> ReadDrawingAsync(string drawingPath, CancellationToken cancellationToken = default)
    {
        if (PetsciiImageConverter.IsSupportedImage(drawingPath))
        {
            var fullPath = Path.GetFullPath(drawingPath);
            var info = new FileInfo(fullPath);
            if (info.Exists)
            {
                if (ImageCache.TryGetValue(fullPath, out var cached)
                    && cached.LastWriteUtc == info.LastWriteTimeUtc
                    && cached.Length == info.Length)
                {
                    return cached.Data;
                }

                var converted = await PetsciiImageConverter.ConvertFileAsync(fullPath, cancellationToken).ConfigureAwait(false);
                ImageCache[fullPath] = new ImageCacheEntry(info.LastWriteTimeUtc, info.Length, converted);
                return converted;
            }
        }

        return await File.ReadAllBytesAsync(drawingPath, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class ZMachineService : IZMachineService
{
    private const string ExitToken = "/exit";
    private const int DefaultColumns = 39;

    public async Task RunAsync(string gameName, byte[] storyData, Func<string, Task> onOutput, Func<Task<string>> onInput, CancellationToken cancellationToken = default)
    {
        var storyPath = Path.Combine(Path.GetTempPath(), $"bbs-zmachine-{Guid.NewGuid():N}.z3");
        await File.WriteAllBytesAsync(storyPath, storyData, cancellationToken).ConfigureAwait(false);

        var interpreter = ResolveInterpreter();

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = interpreter,
                Arguments = QuoteArgument(storyPath),
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.Latin1,
                StandardErrorEncoding = Encoding.Latin1
            }
        };

        try
        {
            if (!process.Start())
            {
                await onOutput("Cannot start Z-Machine interpreter process.\n").ConfigureAwait(false);
                return;
            }
        }
        catch (Exception ex)
        {
            await onOutput("Z-Machine interpreter is unavailable.\n").ConfigureAwait(false);
            await onOutput($"Details: {ex.Message}\n").ConfigureAwait(false);
            await onOutput("Try setting ZMACHINE_INTERPRETER (e.g. /usr/games/dfrotz).\n").ConfigureAwait(false);
            return;
        }

        await onOutput($"Running '{gameName}'. Type {ExitToken} to return to menu.\n\n").ConfigureAwait(false);

        using var cancellationRegistration = cancellationToken.Register(() => TryKill(process));

        var columns = ResolveColumns();

        var outputTask = PumpStreamAsync(process.StandardOutput.BaseStream, onOutput, columns, cancellationToken);
        var errorTask = PumpStreamAsync(process.StandardError.BaseStream, onOutput, columns, cancellationToken);

        try
        {
            while (!cancellationToken.IsCancellationRequested && !process.HasExited)
            {
                var input = await onInput().ConfigureAwait(false);
                if (input is null)
                {
                    continue;
                }

                if (input.Trim().Equals(ExitToken, StringComparison.OrdinalIgnoreCase))
                {
                    TryKill(process);
                    break;
                }

                await process.StandardInput.WriteLineAsync(input).ConfigureAwait(false);
                await process.StandardInput.FlushAsync().ConfigureAwait(false);
            }
        }
        catch (IOException)
        {
            // Process stream closed while client was typing.
        }
        catch (ObjectDisposedException)
        {
            // Process already disposed/closed.
        }
        finally
        {
            try
            {
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }

            try
            {
                await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }

            TryDelete(storyPath);
        }
    }

    private static string ResolveInterpreter()
    {
        var env = Environment.GetEnvironmentVariable("ZMACHINE_INTERPRETER");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env.Trim();
        }

        var candidates = new[]
        {
            "/usr/games/dfrotz",
            "/usr/bin/dfrotz",
            "dfrotz",
            "/usr/games/frotz",
            "/usr/bin/frotz",
            "frotz"
        };

        foreach (var candidate in candidates)
        {
            if (LooksLikePath(candidate))
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                continue;
            }

            if (ExistsInPath(candidate))
            {
                return candidate;
            }
        }

        return "dfrotz";
    }

    private static bool ExistsInPath(string executableName)
    {
        var pathRaw = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathRaw))
        {
            return false;
        }

        foreach (var dir in pathRaw.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var full = Path.Combine(dir, executableName);
                if (File.Exists(full))
                {
                    return true;
                }
            }
            catch
            {
                // ignore invalid path entries
            }
        }

        return false;
    }

    private static bool LooksLikePath(string value)
    {
        return value.Contains('/') || value.Contains('\\');
    }

    private static async Task PumpStreamAsync(Stream stream, Func<string, Task> onOutput, int columns, CancellationToken cancellationToken)
    {
        var buffer = new byte[2048];
        var currentColumn = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read <= 0)
            {
                return;
            }

            var chunk = Encoding.Latin1.GetString(buffer, 0, read);
            var normalized = NormalizeInterpreterOutput(chunk);
            if (normalized.Length == 0)
            {
                continue;
            }

            var wrapped = WrapToColumns(normalized, columns, ref currentColumn);
            if (wrapped.Length == 0)
            {
                continue;
            }

            await onOutput(wrapped).ConfigureAwait(false);
        }
    }

    private static string NormalizeInterpreterOutput(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var normalized = input
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\b", string.Empty, StringComparison.Ordinal)
            .Replace("\0", string.Empty, StringComparison.Ordinal);

        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (ch == '\n')
            {
                sb.Append(ch);
                continue;
            }

            if (ch == '\t')
            {
                sb.Append(' ');
                continue;
            }

            if (char.IsControl(ch))
            {
                continue;
            }

            sb.Append(ch);
        }

        return sb.ToString();
    }


    private static int ResolveColumns()
    {
        var raw = Environment.GetEnvironmentVariable("ZMACHINE_COLUMNS");
        if (int.TryParse(raw, out var parsed) && parsed >= 20 && parsed <= 120)
        {
            return parsed;
        }

        return DefaultColumns;
    }

    private static string WrapToColumns(string input, int columns, ref int currentColumn)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(input.Length + 32);
        foreach (var ch in input)
        {
            if (ch == '\n')
            {
                sb.Append('\n');
                currentColumn = 0;
                continue;
            }

            sb.Append(ch);
            currentColumn++;

            if (currentColumn >= columns)
            {
                sb.Append('\n');
                currentColumn = 0;
            }
        }

        return sb.ToString();
    }
    private static string QuoteArgument(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        return value.Contains(' ') ? $"\"{value}\"" : value;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // ignored
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignored
        }
    }
}









