using Bbs.Core.Content;
using Bbs.Terminals;

namespace Bbs.Tenants.Content;

public class WordpressProxyPetscii : PetsciiThread
{
    protected virtual string Domain => "https://wordpress.org/news";

    protected virtual int PageSize => 10;

    protected virtual bool ShowAuthor => false;

    protected virtual string? CategoriesId => null;

    protected virtual IWordpressService WordpressService => new WordpressService();

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
            Println($"WordPress: {TextRender.TrimTo(Domain, 39)}");
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
        WordpressPostDetails post;
        try
        {
            post = await WordpressService.GetPostAsync(Domain, id, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
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
        lines.AddRange(TextRender.WrapLines(TextRender.SanitizeHtmlToText(post.Content), 39));

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
}
