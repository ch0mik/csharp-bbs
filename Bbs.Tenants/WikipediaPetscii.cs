using Bbs.Core.Content;
using Bbs.Tenants.Content;
using Bbs.Terminals;

namespace Bbs.Tenants;

public sealed class WikipediaPetscii : PetsciiThread
{
    private readonly IWikipediaService _wikipediaService = new WikipediaService();

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

        var offset = 0;
        const int pageRows = 19;
        while (!cancellationToken.IsCancellationRequested)
        {
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
}
