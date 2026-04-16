using Bbs.Core.Content;
using Bbs.Terminals;
using System.Net;
using System.Text.RegularExpressions;

namespace Bbs.Tenants.Content;

public class WordpressProxyPetscii : PetsciiThread
{
    private const int PreferredImageWidth = 1280;
    private const string SessionInlineImagesKey = "session:inline-petscii-images";

    private static readonly Regex ImageTagRegex = new(
        "<img(?<tag>[^>]*)>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    protected virtual string Domain => "https://wordpress.org/news";

    protected virtual int PageSize => 10;

    protected virtual bool ShowAuthor => false;

    protected virtual string? CategoriesId => null;

    protected virtual bool InlineImagesEnabled => InlinePetsciiFeatureFlags.IsWordpressEnabled();
    private bool EffectiveInlineImagesEnabled => InlineImagesEnabled && IsSessionInlineImagesEnabled();

    protected virtual IWordpressService WordpressService => new WordpressService();

    private readonly WordpressImageRenderer _imageRenderer = new();

    public override async Task DoLoopAsync(CancellationToken cancellationToken = default)
    {
        var page = 1;

        while (!cancellationToken.IsCancellationRequested)
        {
            IReadOnlyList<WordpressPostSummary> posts;
            try
            {
                posts = await WordpressService.GetPostsAsync(Domain, page, PageSize, CategoriesId, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Cls();
                Println("WordPress error:");
                Println(TextRender.TrimTo(ex.Message, 39));
                Println();
                Print("Press ENTER...");
                await FlushAsync(cancellationToken).ConfigureAwait(false);
                await ReadLineAsync(maxLength: 1, cancellationToken: cancellationToken).ConfigureAwait(false);
                return;
            }

            Cls();
            Println(TextRender.TrimTo(Domain, 39));
            Println(new string('-', 39));

            foreach (var post in posts)
            {
                var title = TextRender.TrimTo(TextRender.SanitizeHtmlToText(post.Title), 31);
                Println($"#{post.Id} {title}");
            }

            Println();
            Print("# open, N+/N-, R, . > ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);

            var input = (await ReadLineAsync(maxLength: 24, cancellationToken: cancellationToken).ConfigureAwait(false)).Trim();
            if (input is "." or "q" or "Q")
            {
                return;
            }

            if (string.Equals(input, "n+", StringComparison.OrdinalIgnoreCase) || string.Equals(input, "+", StringComparison.OrdinalIgnoreCase) || string.Equals(input, "n", StringComparison.OrdinalIgnoreCase))
            {
                page++;
                continue;
            }

            if ((string.Equals(input, "n-", StringComparison.OrdinalIgnoreCase) || string.Equals(input, "-", StringComparison.OrdinalIgnoreCase)) && page > 1)
            {
                page--;
                continue;
            }

            if (string.Equals(input, "r", StringComparison.OrdinalIgnoreCase) || string.Equals(input, "reload", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!long.TryParse(input.TrimStart('#'), out var id))
            {
                continue;
            }

            await ShowPostAsync(id, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ShowPostAsync(long id, CancellationToken cancellationToken)
    {
        DebugLog($"Open post request: domain='{Domain}', post_id={id}");
        WordpressPostDetails post;
        try
        {
            post = await WordpressService.GetPostAsync(Domain, id, cancellationToken: cancellationToken).ConfigureAwait(false);
            DebugLog($"Post loaded: id={post.Id}, featured_media={post.FeaturedMediaId?.ToString() ?? "null"}, content_len={post.Content?.Length ?? 0}, excerpt_len={post.Excerpt?.Length ?? 0}");
        }
        catch (Exception ex)
        {
            DebugLog($"Open post failed: id={id}, error='{ex.Message}'");
            Cls();
            Println("Open post failed:");
            Println(TextRender.TrimTo(ex.Message, 39));
            Println();
            Print("Press ENTER...");
            await FlushAsync(cancellationToken).ConfigureAwait(false);
            await ReadLineAsync(maxLength: 1, cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        string? author = null;
        if (ShowAuthor && post.AuthorId is { } authorId && authorId > 0)
        {
            try
            {
                author = await WordpressService.GetAuthorNameAsync(Domain, authorId, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                author = null;
            }
        }

        if (EffectiveInlineImagesEnabled)
        {
            var imageUrls = await GetInlineImageUrlsAsync(post, cancellationToken).ConfigureAwait(false);
            DebugLog($"Inline image candidates: post_id={post.Id}, count={imageUrls.Count}");
            if (imageUrls.Count > 0)
            {
                DebugLog($"Inline image preview: {string.Join(" | ", imageUrls.Take(3))}");
            }
            if (imageUrls.Count > 0)
            {
                var continueToText = await RenderInlineImagesAsync(imageUrls, cancellationToken).ConfigureAwait(false);
                if (!continueToText)
                {
                    DebugLog($"User left post from inline image screen: post_id={post.Id}");
                    return;
                }
            }
        }
        else if (!InlineImagesEnabled)
        {
            DebugLog($"Inline images disabled by config for WordPress. post_id={post.Id}");
        }
        else
        {
            DebugLog($"Inline images disabled by session toggle. post_id={post.Id}");
        }

        var lines = new List<string>();
        lines.AddRange(TextRender.WrapLines(TextRender.SanitizeHtmlToText(post.Title), 39));
        if (post.PublishedAt is not null)
        {
            lines.Add(post.PublishedAt.Value.ToString("yyyy-MM-dd HH:mm"));
        }

        if (!string.IsNullOrWhiteSpace(author))
        {
            lines.Add("Author: " + author);
        }

        lines.Add(new string('-', 39));
        lines.AddRange(TextRender.WrapLines(TextRender.SanitizeHtmlToText(post.Content ?? string.Empty), 39));

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

            if ((key is "-" or "P") && offset > 0)
            {
                offset = Math.Max(0, offset - pageRows);
                continue;
            }

            if (offset + pageRows < lines.Count)
            {
                offset += pageRows;
            }
        }
    }

    private async Task<bool> RenderInlineImagesAsync(IReadOnlyList<string> imageUrls, CancellationToken cancellationToken)
    {
        if (imageUrls.Count == 0)
        {
            DebugLog("RenderInlineImagesAsync: no image URLs.");
            return true;
        }

        var renderableUrls = imageUrls.Where(IsLikelyRenderableRasterUrl).ToList();
        DebugLog($"Renderable raster candidates: total={imageUrls.Count}, raster_like={renderableUrls.Count}");
        if (renderableUrls.Count == 0)
        {
            DebugLog("RenderInlineImagesAsync: no raster-like URLs.");
            return true;
        }

        for (var i = 0; i < renderableUrls.Count; i++)
        {
            var url = renderableUrls[i];

            byte[] data;
            try
            {
                data = await _imageRenderer.RenderAsync(url, cancellationToken).ConfigureAwait(false);
                DebugLog($"Image render attempt: url='{url}', petscii_bytes={data.Length}");
            }
            catch (Exception ex)
            {
                DebugLog($"Image render failed: url='{url}', error='{ex.Message}'");
                continue;
            }

            if (data.Length == 0)
            {
                DebugLog($"Image render empty output: url='{url}'");
                continue;
            }

            Cls();
            Write(data);
            await NormalizeTextModeAsync(cancellationToken).ConfigureAwait(false);
            Write(PetsciiKeys.Return);
            Println();
            Print($"Image {i + 1}/{renderableUrls.Count}  ENTER=Next  T=Text  .=Back > ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);

            var key = (await ReadLineAsync(maxLength: 2, cancellationToken: cancellationToken).ConfigureAwait(false)).Trim().ToUpperInvariant();
            if (key is "." or "Q")
            {
                DebugLog($"User aborted inline image flow on image {i + 1}/{renderableUrls.Count}.");
                return false;
            }

            if (key is "T" or "S")
            {
                DebugLog("User switched from inline images to text.");
                return true;
            }

            await NormalizeTextModeAsync(cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    private async Task<IReadOnlyList<string>> GetInlineImageUrlsAsync(WordpressPostDetails post, CancellationToken cancellationToken)
    {
        var urls = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (post.FeaturedMediaId is { } mediaId && mediaId > 0)
        {
            try
            {
                var mediaUrl = await WordpressService.GetMediaUrlAsync(Domain, mediaId, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(mediaUrl) && seen.Add(mediaUrl))
                {
                    urls.Add(mediaUrl);
                    DebugLog($"Featured media URL added: media_id={mediaId}, url='{mediaUrl}'");
                }
            }
            catch (Exception ex)
            {
                DebugLog($"Featured media lookup failed: media_id={mediaId}, error='{ex.Message}'");
            }
        }

        foreach (var url in ExtractImageUrls(post.Content, Domain))
        {
            if (seen.Add(url))
            {
                urls.Add(url);
            }
        }

        if (urls.Count == 0)
        {
            foreach (var url in ExtractImageUrls(post.Excerpt, Domain))
            {
                if (seen.Add(url))
                {
                    urls.Add(url);
                }
            }
        }

        return urls;
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

    private static IReadOnlyList<string> ExtractImageUrls(string html, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            DebugLog("ExtractImageUrls: empty HTML.");
            return Array.Empty<string>();
        }

        Uri? baseUri = null;
        if (!string.IsNullOrWhiteSpace(baseUrl) && Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsed))
        {
            baseUri = parsed;
        }
        DebugLog($"ExtractImageUrls: base_url='{baseUrl}', base_uri='{baseUri?.ToString() ?? "null"}'");

        var urls = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var totalTags = 0;
        var missingSource = 0;
        var normalizeFailed = 0;
        var duplicates = 0;

        foreach (Match match in ImageTagRegex.Matches(html))
        {
            totalTags++;
            var attrs = ParseAttributes(match.Groups["tag"].Value);
            if (!TryPickBestImageSource(attrs, out var raw))
            {
                missingSource++;
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
                normalizeFailed++;
                continue;
            }

            if (seen.Add(normalized))
            {
                urls.Add(normalized);
            }
            else
            {
                duplicates++;
            }
        }

        DebugLog($"ExtractImageUrls summary: tags={totalTags}, selected={urls.Count}, no_source={missingSource}, normalize_failed={normalizeFailed}, duplicates={duplicates}");
        if (urls.Count > 0)
        {
            DebugLog($"ExtractImageUrls selected preview: {string.Join(" | ", urls.Take(3))}");
        }

        return urls;
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

        if (TryGetBestFromSrcset(attrs, "data-lazy-srcset", out source))
        {
            return true;
        }

        foreach (var key in new[] { "src", "data-src", "data-lazy-src", "data-original", "data-orig-file", "data-large-file" })
        {
            if (attrs.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                source = value;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetBestFromSrcset(IReadOnlyDictionary<string, string> attrs, string key, out string source)
    {
        source = string.Empty;
        if (!attrs.TryGetValue(key, out var srcsetRaw) || string.IsNullOrWhiteSpace(srcsetRaw))
        {
            return false;
        }

        var bestAbovePreferredWidth = int.MaxValue;
        string? bestAbovePreferred = null;
        var bestFallbackWidth = -1;
        string? bestFallback = null;

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
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            if (width >= PreferredImageWidth && width < bestAbovePreferredWidth)
            {
                bestAbovePreferredWidth = width;
                bestAbovePreferred = url;
            }

            if (width > bestFallbackWidth)
            {
                bestFallbackWidth = width;
                bestFallback = url;
            }
        }

        source = bestAbovePreferred ?? bestFallback ?? string.Empty;
        return source.Length > 0;
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

    private static string? NormalizeImageUrl(string raw, Uri? baseUri)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim();
        if (value.StartsWith("//", StringComparison.Ordinal))
        {
            value = (baseUri?.Scheme ?? Uri.UriSchemeHttps) + ":" + value;
        }

        Uri? absolute = null;
        if (value.StartsWith("/", StringComparison.Ordinal) && !value.StartsWith("//", StringComparison.Ordinal))
        {
            if (baseUri is null || !Uri.TryCreate(baseUri, value, out absolute))
            {
                return null;
            }
        }
        else if (!Uri.TryCreate(value, UriKind.Absolute, out absolute))
        {
            if (baseUri is null || !Uri.TryCreate(baseUri, value, out absolute))
            {
                return null;
            }
        }

        if (absolute is null)
        {
            return null;
        }

        if (absolute.Scheme != Uri.UriSchemeHttp && absolute.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        return absolute.AbsoluteUri;
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

    private static void DebugLog(string message)
    {
        Console.WriteLine($"[{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}][WordpressProxyPetscii] {message}");
    }
}
