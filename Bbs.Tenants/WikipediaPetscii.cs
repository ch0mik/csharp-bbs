using Bbs.Core.Content;
using Bbs.Tenants.Content;
using Bbs.Terminals;
using System.Net;
using System.Text.RegularExpressions;

namespace Bbs.Tenants;

public sealed class WikipediaPetscii : PetsciiThread
{
    private const int MinInlineImageWidth = 320;
    private const int MinInlineImageHeight = 200;
    private const string SessionInlineImagesKey = "session:inline-petscii-images";

    private static readonly Regex ImageTagRegex = new(
        "<img(?<tag>[^>]*)>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex InfoboxRegex = new(
        "<table[^>]*class\\s*=\\s*['\"][^'\"]*infobox[^'\"]*['\"][^>]*>(?<body>.*?)</table>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex FigureRegex = new(
        "<figure[^>]*>(?<body>.*?)</figure>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex ThumbWidthRegex = new(
        "/(?<n>\\d{2,5})px-[^/]+$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IWikipediaService _wikipediaService = new WikipediaService();
    private readonly WikipediaImageRenderer _imageRenderer = new();
    private readonly bool _inlineImagesEnabled = InlinePetsciiFeatureFlags.IsWikipediaEnabled();

    private string _lang = "en";


    public override async Task DoLoopAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Cls();
            Println("Wikipedia PETSCII");
            Println(new string('-', 39));
            Println($"Lang: {_lang.ToUpperInvariant()}");
            Println("1) Search");
            Println("2) Random page");
            Println("3) Change lang");
            Println(".) Back");
            Print("Choice: ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);

            var choice = (await ReadLineAsync(maxLength: 4, cancellationToken: cancellationToken).ConfigureAwait(false)).Trim();
            if (choice == ".")
            {
                return;
            }

            if (choice == "1")
            {
                await SearchFlowAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (choice == "2")
            {
                await RandomFlowAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (choice == "3")
            {
                await ChangeLangAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task SearchFlowAsync(CancellationToken cancellationToken)
    {
        Cls();
        Print("Query: ");
        await FlushAsync(cancellationToken).ConfigureAwait(false);
        var query = (await ReadLineAsync(maxLength: 64, cancellationToken: cancellationToken).ConfigureAwait(false)).Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        IReadOnlyList<WikipediaSearchItem> items;
        try
        {
            items = await _wikipediaService.SearchAsync(_lang, query, 20, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (items.Count == 0)
        {
            Cls();
            Println("No result.");
            Print("Press ENTER...");
            await FlushAsync(cancellationToken).ConfigureAwait(false);
            await ReadLineAsync(maxLength: 1, cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            Cls();
            Println("Search results:");
            Println(new string('-', 39));
            for (var i = 0; i < Math.Min(items.Count, 9); i++)
            {
                var title = TextRender.TrimTo(TextRender.SanitizeHtmlToText(items[i].Title), 34);
                Println($"{i + 1}) {title}");
            }

            Println(".) Back");
            Print("Select item: ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);
            var input = (await ReadLineAsync(maxLength: 2, cancellationToken: cancellationToken).ConfigureAwait(false)).Trim();
            if (input == ".")
            {
                return;
            }

            if (!int.TryParse(input, out var idx) || idx < 1 || idx > Math.Min(items.Count, 9))
            {
                continue;
            }

            await ShowArticleAsync(items[idx - 1], cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RandomFlowAsync(CancellationToken cancellationToken)
    {
        try
        {
            var random = await _wikipediaService.PickRandomAsync(_lang, cancellationToken).ConfigureAwait(false);
            if (random is not null)
            {
                await ShowArticleAsync(random, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ShowArticleAsync(WikipediaSearchItem item, CancellationToken cancellationToken)
    {
        string html;
        try
        {
            html = await _wikipediaService.GetArticleHtmlAsync(item.Language, item.PageId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex, cancellationToken).ConfigureAwait(false);
            return;
        }

        var lines = new List<string>();
        lines.AddRange(TextRender.WrapLines(item.Title, 39));
        lines.Add(new string('-', 39));
        lines.AddRange(TextRender.WrapLines(TextRender.SanitizeHtmlToText(html), 39));

        if (!_inlineImagesEnabled)
        {
            DebugLog("Inline PETSCII images disabled by config for Wikipedia.");
        }
        else if (!IsSessionInlineImagesEnabled())
        {
            DebugLog("Inline PETSCII images disabled by session toggle for Wikipedia.");
        }
        else
        {
            var articleUrl = BuildArticleUrl(item.Language, item.PageId);
            var imageUrls = ExtractInfoboxImageUrls(html, articleUrl);
            DebugLog($"Wiki infobox image URLs: count={imageUrls.Count}");
            if (imageUrls.Count == 0)
            {
                imageUrls = ExtractFigureImageUrls(html, articleUrl);
                DebugLog($"Wiki figure image URLs fallback: count={imageUrls.Count}");
            }

            var continueToArticle = await RenderInlineImagesAsync(imageUrls, cancellationToken).ConfigureAwait(false);
            if (!continueToArticle)
            {
                return;
            }
        }

        var offset = 0;
        const int pageRows = 19;
        while (!cancellationToken.IsCancellationRequested)
        {
            await NormalizeTextModeAsync(cancellationToken).ConfigureAwait(false);
            Cls();
            foreach (var row in lines.Skip(offset).Take(pageRows))
            {
                Println(row);
            }

            Println();
            Print("N=Next  -=Prev  .=Back > ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);
            var key = (await ReadLineAsync(maxLength: 2, cancellationToken: cancellationToken).ConfigureAwait(false)).Trim().ToUpperInvariant();
            if (key is "." or "Q")
            {
                return;
            }

            if (key is "-" or "P")
            {
                if (offset > 0)
                {
                    offset = Math.Max(0, offset - pageRows);
                }
                continue;
            }

            if (key is "N" or "")
            {
                if (offset + pageRows < lines.Count)
                {
                    offset += pageRows;
                }
                continue;
            }
        }
    }

    private async Task<bool> RenderInlineImagesAsync(IReadOnlyList<string> imageUrls, CancellationToken cancellationToken)
    {
        if (imageUrls.Count == 0)
        {
            DebugLog("No inline images found for wiki article.");
            return true;
        }

        var renderableUrls = imageUrls
            .Where(IsLikelyRenderableRasterUrl)
            .ToList();
        DebugLog($"Wiki image renderable candidates: total={imageUrls.Count}, raster_like={renderableUrls.Count}");

        if (renderableUrls.Count == 0)
        {
            DebugLog("No wiki image candidate looks like a renderable raster file.");
            return true;
        }

        var shown = 0;
        for (var i = 0; i < renderableUrls.Count; i++)
        {
            var url = renderableUrls[i];
            DebugLog($"Inline wiki image candidate {i + 1}/{renderableUrls.Count}: {url}");

            byte[] data;
            try
            {
                data = await _imageRenderer.RenderAsync(url, cancellationToken).ConfigureAwait(false);
                DebugLog($"Wiki image render attempt: url='{url}', petscii_bytes={data.Length}");
            }
            catch (Exception ex)
            {
                DebugLog($"Wiki image render failed: url='{url}', error='{ex.Message}'");
                continue;
            }

            if (data.Length == 0)
            {
                DebugLog($"Wiki image rendered empty output: url='{url}'");
                continue;
            }

            Cls();
            Write(data);
            await NormalizeTextModeAsync(cancellationToken).ConfigureAwait(false);
            Write(PetsciiKeys.Return);
            Println();
            Print($"Image {shown + 1}/{renderableUrls.Count}  ENTER=Next  T=Text  .=Back > ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);

            var key = (await ReadLineAsync(maxLength: 2, cancellationToken: cancellationToken).ConfigureAwait(false)).Trim().ToUpperInvariant();
            if (key is "." or "Q")
            {
                return false;
            }
            if (key is "T" or "S")
            {
                return true;
            }

            await NormalizeTextModeAsync(cancellationToken).ConfigureAwait(false);
            shown++;
        }

        if (shown == 0)
        {
            DebugLog("No wiki image candidate produced a non-empty PETSCII result.");
        }

        return true;
    }

    private static bool IsLikelyRenderableRasterUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var lower = url.ToLowerInvariant();
        return lower.Contains(".jpg", StringComparison.Ordinal)
            || lower.Contains(".jpeg", StringComparison.Ordinal)
            || lower.Contains(".png", StringComparison.Ordinal)
            || lower.Contains(".gif", StringComparison.Ordinal)
            || lower.Contains(".webp", StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> ExtractImageUrls(string html, string articleUrl)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            DebugLog("Wiki image extraction: empty HTML.");
            return Array.Empty<string>();
        }

        var urls = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var totalTags = 0;
        var noSource = 0;
        var filteredByOriginalWidth = 0;
        var filteredByOriginalHeight = 0;
        var filteredByThumbWidth = 0;
        var invalidUri = 0;
        var unsupportedScheme = 0;
        var duplicate = 0;

        Uri? baseUri = null;
        if (!string.IsNullOrWhiteSpace(articleUrl) && Uri.TryCreate(articleUrl, UriKind.Absolute, out var parsed))
        {
            baseUri = parsed;
        }

        foreach (Match match in ImageTagRegex.Matches(html))
        {
            totalTags++;
            var tag = match.Groups["tag"].Value;
            var attrs = ParseAttributes(tag);
            if (!TryPickBestImageSource(attrs, out var raw))
            {
                noSource++;
                continue;
            }

            var candidate = WebUtility.HtmlDecode(raw.Trim()).Replace("\\/", "/", StringComparison.Ordinal);

            if (candidate.Contains(' ', StringComparison.Ordinal))
            {
                candidate = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? candidate;
            }

            if (candidate.StartsWith("//", StringComparison.Ordinal))
            {
                candidate = "https:" + candidate;
            }

            if (candidate.StartsWith("/", StringComparison.Ordinal) && baseUri is not null)
            {
                candidate = new Uri(baseUri, candidate).ToString();
            }

            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
            {
                invalidUri++;
                DebugLog($"Wiki image rejected: invalid_uri raw='{candidate}'");
                continue;
            }

            var scheme = uri.Scheme.ToLowerInvariant();
            if (scheme != "http" && scheme != "https")
            {
                unsupportedScheme++;
                DebugLog($"Wiki image rejected: unsupported_scheme scheme='{scheme}' url='{uri}'");
                continue;
            }

            var normalized = uri.ToString();

            if (attrs.TryGetValue("data-file-width", out var dataWidthRaw)
                && int.TryParse(dataWidthRaw, out var dataWidth)
                && dataWidth < MinInlineImageWidth)
            {
                filteredByOriginalWidth++;
                DebugLog($"Wiki image rejected: data-file-width too small ({dataWidth} < {MinInlineImageWidth}) url='{normalized}'");
                continue;
            }

            if (attrs.TryGetValue("data-file-height", out var dataHeightRaw)
                && int.TryParse(dataHeightRaw, out var dataHeight)
                && dataHeight < MinInlineImageHeight)
            {
                filteredByOriginalHeight++;
                DebugLog($"Wiki image rejected: data-file-height too small ({dataHeight} < {MinInlineImageHeight}) url='{normalized}'");
                continue;
            }

            if (TryReadInt(ThumbWidthRegex, normalized, out var thumbWidth) && thumbWidth < MinInlineImageWidth)
            {
                filteredByThumbWidth++;
                DebugLog($"Wiki image rejected: thumbnail width too small ({thumbWidth} < {MinInlineImageWidth}) url='{normalized}'");
                continue;
            }

            if (seen.Add(normalized))
            {
                urls.Add(normalized);
                DebugLog($"Wiki image accepted: url='{normalized}'");
            }
            else
            {
                duplicate++;
            }
        }

        DebugLog(
            $"Wiki image extraction summary: tags={totalTags}, selected={urls.Count}, no_source={noSource}, " +
            $"filtered_orig_w={filteredByOriginalWidth}, filtered_orig_h={filteredByOriginalHeight}, " +
            $"filtered_thumb_w={filteredByThumbWidth}, invalid_uri={invalidUri}, unsupported_scheme={unsupportedScheme}, duplicates={duplicate}");

        for (var i = 0; i < urls.Count; i++)
        {
            DebugLog($"Wiki image selected url[{i + 1}/{urls.Count}]: {urls[i]}");
        }

        return urls;
    }

    private static IReadOnlyList<string> ExtractInfoboxImageUrls(string html, string articleUrl)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return Array.Empty<string>();
        }

        var urls = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match box in InfoboxRegex.Matches(html))
        {
            var scoped = ExtractImageUrls(box.Groups["body"].Value, articleUrl);
            foreach (var url in scoped)
            {
                if (seen.Add(url))
                {
                    urls.Add(url);
                }
            }
        }

        for (var i = 0; i < urls.Count; i++)
        {
            DebugLog($"Wiki infobox selected url[{i + 1}/{urls.Count}]: {urls[i]}");
        }

        return urls;
    }

    private static IReadOnlyList<string> ExtractFigureImageUrls(string html, string articleUrl)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return Array.Empty<string>();
        }

        var urls = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match figure in FigureRegex.Matches(html))
        {
            var scoped = ExtractImageUrls(figure.Groups["body"].Value, articleUrl);
            foreach (var url in scoped)
            {
                if (seen.Add(url))
                {
                    urls.Add(url);
                }
            }
        }

        for (var i = 0; i < urls.Count; i++)
        {
            DebugLog($"Wiki figure selected url[{i + 1}/{urls.Count}]: {urls[i]}");
        }

        return urls;
    }

    private static string BuildArticleUrl(string language, long pageId)
    {
        var lang = string.IsNullOrWhiteSpace(language) ? "en" : language.Trim().ToLowerInvariant();
        return $"https://{lang}.wikipedia.org/?curid={pageId}";
    }

    private static bool TryReadInt(Regex regex, string input, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var match = regex.Match(input);
        if (!match.Success)
        {
            return false;
        }

        return int.TryParse(match.Groups["n"].Value, out value) && value > 0;
    }

    private static bool TryPickBestImageSource(IReadOnlyDictionary<string, string> attrs, out string source)
    {
        source = string.Empty;

        if (TryGetBestFromSrcset(attrs, "srcset", out source))
        {
            return true;
        }

        if (TryGetBestFromSrcset(attrs, "data-srcset", out source))
        {
            return true;
        }

        if (attrs.TryGetValue("src", out var src) && !string.IsNullOrWhiteSpace(src))
        {
            source = src;
            return true;
        }

        if (attrs.TryGetValue("data-src", out var dataSrc) && !string.IsNullOrWhiteSpace(dataSrc))
        {
            source = dataSrc;
            return true;
        }

        return false;
    }

    private static Dictionary<string, string> ParseAttributes(string imgTag)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(imgTag))
        {
            return result;
        }

        var i = 0;
        while (i < imgTag.Length)
        {
            while (i < imgTag.Length && char.IsWhiteSpace(imgTag[i]))
            {
                i++;
            }

            var nameStart = i;
            while (i < imgTag.Length && !char.IsWhiteSpace(imgTag[i]) && imgTag[i] != '=' && imgTag[i] != '/' && imgTag[i] != '>')
            {
                i++;
            }

            if (i <= nameStart)
            {
                i++;
                continue;
            }

            var name = imgTag[nameStart..i].Trim();
            while (i < imgTag.Length && char.IsWhiteSpace(imgTag[i]))
            {
                i++;
            }

            if (i >= imgTag.Length || imgTag[i] != '=')
            {
                continue;
            }

            i++;
            while (i < imgTag.Length && char.IsWhiteSpace(imgTag[i]))
            {
                i++;
            }

            if (i >= imgTag.Length)
            {
                break;
            }

            string value;
            var quote = imgTag[i];
            if (quote is '"' or '\'')
            {
                i++;
                var valueStart = i;
                while (i < imgTag.Length && imgTag[i] != quote)
                {
                    i++;
                }

                value = imgTag[valueStart..Math.Min(i, imgTag.Length)];
                if (i < imgTag.Length && imgTag[i] == quote)
                {
                    i++;
                }
            }
            else
            {
                var valueStart = i;
                while (i < imgTag.Length && !char.IsWhiteSpace(imgTag[i]) && imgTag[i] != '>' && imgTag[i] != '/')
                {
                    i++;
                }

                value = imgTag[valueStart..Math.Min(i, imgTag.Length)];
            }

            if (name.Length > 0 && value.Length > 0)
            {
                result[name] = value;
            }
        }

        return result;
    }

    private static bool TryGetBestFromSrcset(IReadOnlyDictionary<string, string> attrs, string key, out string source)
    {
        source = string.Empty;
        if (!attrs.TryGetValue(key, out var srcsetRaw) || string.IsNullOrWhiteSpace(srcsetRaw))
        {
            return false;
        }

        var bestWidth = -1;
        var bestAnyWidth = -1;
        string? best = null;
        string? bestAny = null;

        foreach (var part in srcsetRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pieces = part.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (pieces.Length == 0)
            {
                continue;
            }

            var url = pieces[0].Trim();
            var width = 0;
            if (pieces.Length > 1)
            {
                var token = pieces[^1];
                if (token.EndsWith("w", StringComparison.OrdinalIgnoreCase))
                {
                    _ = int.TryParse(token[..^1], out width);
                }
                else if (token.EndsWith("x", StringComparison.OrdinalIgnoreCase)
                    && double.TryParse(token[..^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var scale)
                    && TryReadInt(ThumbWidthRegex, url, out var thumbFromUrl)
                    && scale > 0)
                {
                    width = (int)Math.Round(thumbFromUrl * scale);
                }
            }

            if (width == 0 && TryReadInt(ThumbWidthRegex, url, out var fallbackWidth))
            {
                width = fallbackWidth;
            }

            if (width > bestAnyWidth)
            {
                bestAnyWidth = width;
                bestAny = url;
            }

            if (width >= MinInlineImageWidth && width > bestWidth)
            {
                bestWidth = width;
                best = url;
            }
        }

        source = best ?? bestAny ?? string.Empty;
        return source.Length > 0;
    }

    private async Task ChangeLangAsync(CancellationToken cancellationToken)
    {
        Cls();
        Print("Lang (e.g. en, it, pl): ");
        await FlushAsync(cancellationToken).ConfigureAwait(false);
        var lang = (await ReadLineAsync(maxLength: 8, cancellationToken: cancellationToken).ConfigureAwait(false)).Trim();
        if (!string.IsNullOrWhiteSpace(lang))
        {
            _lang = new string(lang.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(_lang))
            {
                _lang = "en";
            }
        }
    }

    private async Task ShowErrorAsync(Exception ex, CancellationToken cancellationToken)
    {
        Cls();
        Println("Wikipedia error:");
        Println(TextRender.TrimTo(ex.Message, 39));
        Println();
        Print("Press ENTER...");
        await FlushAsync(cancellationToken).ConfigureAwait(false);
        await ReadLineAsync(maxLength: 1, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static void DebugLog(string message)
    {
        Console.WriteLine($"[{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}][WikipediaPetscii] {message}");
    }

    private async Task NormalizeTextModeAsync(CancellationToken cancellationToken)
    {
        Write(
            PetsciiKeys.ReverseOff,
            PetsciiKeys.White,
            PetsciiKeys.Lowercase);
        await FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private bool IsSessionInlineImagesEnabled()
    {
        var value = GetCustomObject(SessionInlineImagesKey);
        return value is not bool b || b;
    }
}
