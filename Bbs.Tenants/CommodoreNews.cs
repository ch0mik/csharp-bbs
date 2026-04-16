using Bbs.Tenants.Content;
using Bbs.Terminals;

namespace Bbs.Tenants;

public sealed class CommodoreNews : PetsciiThread
{
    private const int MenuPageSize = 9;
    private const int ReaderRows = 19;
    private const int MaxInlineImages = 1;
    private const string SessionInlineImagesKey = "session:inline-petscii-images";

    private readonly CommodoreNewsService _service = new();
    private readonly CommodoreNewsImageRenderer _imageRenderer = new();
    private readonly bool _inlineImagesEnabled = InlinePetsciiFeatureFlags.IsCommodoreNewsEnabled();

    private sealed record StyledLine(string Text, int Color);

    public override async Task DoLoopAsync(CancellationToken cancellationToken = default)
    {
        var page = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            await NormalizeTextModeAsync(cancellationToken).ConfigureAwait(false);
            IReadOnlyList<CommodoreNewsItem> allItems;
            try
            {
                allItems = await _service.GetLatestAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Cls();
                Println("CommodoreNews error:");
                Println(TextRender.TrimTo(ex.Message, 39));
                Println();
                Print("Press ENTER...");
                await FlushAsync(cancellationToken).ConfigureAwait(false);
                await ReadLineAsync(maxLength: 1, cancellationToken: cancellationToken).ConfigureAwait(false);
                return;
            }

            var totalPages = Math.Max(1, (int)Math.Ceiling(allItems.Count / (double)MenuPageSize));
            page = Math.Clamp(page, 0, totalPages - 1);
            var pageItems = allItems.Skip(page * MenuPageSize).Take(MenuPageSize).ToArray();

            Cls();
            Write(PetsciiKeys.LightBlue);
            Println("Commodore.net News");
            Write(PetsciiKeys.White);
            Println(new string('-', 39));
            Println($"Page {page + 1}/{totalPages}");
            Println();

            for (var i = 0; i < pageItems.Length; i++)
            {
                Println($"{i + 1}) {TextRender.TrimTo(pageItems[i].Title, 35)}");
            }

            if (pageItems.Length == 0)
            {
                Println("No news entries found.");
            }

            Println();
            Print("1-9=open N+/N- R . > ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);

            var input = (await ReadLineAsync(maxLength: 8, cancellationToken: cancellationToken).ConfigureAwait(false)).Trim();
            if (input is "." or "q" or "Q")
            {
                return;
            }

            if (string.Equals(input, "r", StringComparison.OrdinalIgnoreCase)
                || string.Equals(input, "reload", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(input, "n+", StringComparison.OrdinalIgnoreCase)
                || string.Equals(input, "+", StringComparison.OrdinalIgnoreCase)
                || string.Equals(input, "n", StringComparison.OrdinalIgnoreCase))
            {
                if (page + 1 < totalPages)
                {
                    page++;
                }

                continue;
            }

            if (string.Equals(input, "n-", StringComparison.OrdinalIgnoreCase)
                || string.Equals(input, "-", StringComparison.OrdinalIgnoreCase)
                || string.Equals(input, "p", StringComparison.OrdinalIgnoreCase))
            {
                if (page > 0)
                {
                    page--;
                }

                continue;
            }

            if (!int.TryParse(input, out var idx) || idx < 1 || idx > pageItems.Length)
            {
                continue;
            }

            await ShowArticleAsync(pageItems[idx - 1], cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ShowArticleAsync(CommodoreNewsItem item, CancellationToken cancellationToken)
    {
        CommodoreNewsArticle article;
        try
        {
            article = await _service.GetArticleAsync(item.Url, cancellationToken).ConfigureAwait(false);
            DebugLog($"Article loaded: url='{item.Url}', title='{article.Title}', images_found={article.ImageUrls.Count}");
            if (article.ImageUrls.Count > 0)
            {
                DebugLog($"First image candidate: {article.ImageUrls[0]}");
            }
        }
        catch (Exception ex)
        {
            DebugLog($"Open article failed for '{item.Url}': {ex.Message}");
            Cls();
            Println("Open article failed:");
            Println(TextRender.TrimTo(ex.Message, 39));
            Println();
            Print("Press ENTER...");
            await FlushAsync(cancellationToken).ConfigureAwait(false);
            await ReadLineAsync(maxLength: 1, cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!_inlineImagesEnabled)
        {
            DebugLog("Inline PETSCII images disabled by config for CommodoreNews.");
        }
        else if (!IsSessionInlineImagesEnabled())
        {
            DebugLog("Inline PETSCII images disabled by session toggle.");
        }
        else
        {
            var continueToArticle = await RenderInlineImagesAsync(article.ImageUrls, cancellationToken).ConfigureAwait(false);
            if (!continueToArticle)
            {
                return;
            }
        }

        var lines = BuildStyledLines(article);
        var offset = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            await NormalizeTextModeAsync(cancellationToken).ConfigureAwait(false);
            Cls();
            foreach (var row in lines.Skip(offset).Take(ReaderRows))
            {
                if (string.IsNullOrEmpty(row.Text))
                {
                    Println();
                    continue;
                }

                Write(row.Color);
                Println(row.Text);
            }

            Write(PetsciiKeys.White);
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
                offset = Math.Max(0, offset - ReaderRows);
                continue;
            }

            if (offset + ReaderRows < lines.Count)
            {
                offset += ReaderRows;
            }
        }
    }

    private async Task<bool> RenderInlineImagesAsync(IReadOnlyList<string> imageUrls, CancellationToken cancellationToken)
    {
        if (imageUrls.Count == 0)
        {
            DebugLog("No inline images found for article.");
            return true;
        }

        var shown = 0;
        for (var i = 0; i < imageUrls.Count && shown < MaxInlineImages; i++)
        {
            var url = imageUrls[i];
            DebugLog($"Inline image candidate {i + 1}/{imageUrls.Count}: {url}");
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
                DebugLog($"Image rendered empty output: url='{url}'");
                continue;
            }

            Cls();
            Write(data);
            await NormalizeTextModeAsync(cancellationToken).ConfigureAwait(false);
            Write(PetsciiKeys.Return);
            Println();
            Print("ENTER=Tekst  .=Back > ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);

            var key = (await ReadLineAsync(maxLength: 2, cancellationToken: cancellationToken).ConfigureAwait(false)).Trim().ToUpperInvariant();
            if (key is "." or "Q")
            {
                return false;
            }

            await NormalizeTextModeAsync(cancellationToken).ConfigureAwait(false);

            shown++;
        }

        if (shown == 0)
        {
            DebugLog("No inline image candidate produced a non-empty PETSCII result.");
        }

        return true;
    }

    private static List<StyledLine> BuildStyledLines(CommodoreNewsArticle article)
    {
        var lines = new List<StyledLine>();

        foreach (var block in article.Blocks)
        {
            var color = block.Kind switch
            {
                "h1" => PetsciiKeys.Yellow,
                "h2" => PetsciiKeys.Cyan,
                "h3" => PetsciiKeys.LightGreen,
                "div" => PetsciiKeys.LightGray,
                "span" => PetsciiKeys.Gray,
                _ => PetsciiKeys.White
            };

            if (block.Kind is "h1" or "h2" or "h3")
            {
                if (lines.Count > 0)
                {
                    lines.Add(new StyledLine(string.Empty, PetsciiKeys.White));
                }

                foreach (var row in TextRender.WrapLines(block.Text, 39))
                {
                    lines.Add(new StyledLine(row, color));
                }

                lines.Add(new StyledLine(string.Empty, PetsciiKeys.White));
                continue;
            }

            foreach (var row in TextRender.WrapLines(block.Text, 39))
            {
                lines.Add(new StyledLine(row, color));
            }

            lines.Add(new StyledLine(string.Empty, PetsciiKeys.White));
        }

        return lines;
    }

    private static void DebugLog(string message)
    {
        Console.WriteLine($"[{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}][CommodoreNews] {message}");
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
