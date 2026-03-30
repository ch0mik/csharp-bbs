using Bbs.Tenants.Content;
using Bbs.Terminals;

namespace Bbs.Tenants;

public sealed class CommodoreNews : PetsciiThread
{
    private const int MenuPageSize = 9;
    private const int ReaderRows = 19;

    private readonly CommodoreNewsService _service = new();

    private sealed record StyledLine(string Text, int Color);

    public override async Task DoLoopAsync(CancellationToken cancellationToken = default)
    {
        var page = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
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
        }
        catch (Exception ex)
        {
            Cls();
            Println("Open article failed:");
            Println(TextRender.TrimTo(ex.Message, 39));
            Println();
            Print("Press ENTER...");
            await FlushAsync(cancellationToken).ConfigureAwait(false);
            await ReadLineAsync(maxLength: 1, cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        var lines = BuildStyledLines(article);
        var offset = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
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
}



