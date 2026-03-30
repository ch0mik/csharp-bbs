using Bbs.Core.Content;
using Bbs.Terminals;

namespace Bbs.Tenants.Content;

public abstract class RssPetsciiBase : PetsciiThread
{
    protected virtual int PageRows => 19;

    protected abstract string Title { get; }

    protected abstract IReadOnlyDictionary<string, (string Label, string Url)> Channels { get; }

    protected virtual IRssService RssService => new RssService();

    public override async Task DoLoopAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Cls();
            Println(Title);
            Println(new string('-', 39));

            foreach (var channel in Channels.OrderBy(c => c.Key, StringComparer.OrdinalIgnoreCase))
            {
                Println($"{channel.Key}) {channel.Value.Label}");
            }

            Println(".) Back");
            Print("Choice: ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);

            var choice = (await ReadLineAsync(maxLength: 4, cancellationToken: cancellationToken).ConfigureAwait(false)).Trim();
            if (choice == ".")
            {
                return;
            }

            if (!Channels.TryGetValue(choice, out var selected))
            {
                continue;
            }

            try
            {
                var entries = await RssService.ReadFeedAsync(selected.Url, cancellationToken).ConfigureAwait(false);
                await ShowEntriesAsync(selected.Label, entries, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Cls();
                Println("RSS error:");
                Println(TextRender.TrimTo(ex.Message, 39));
                Println();
                Print("Press ENTER...");
                await FlushAsync(cancellationToken).ConfigureAwait(false);
                await ReadLineAsync(maxLength: 1, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ShowEntriesAsync(string channelName, IReadOnlyList<RssEntry> entries, CancellationToken cancellationToken)
    {
        var lines = new List<string> { channelName, new string('-', 39), string.Empty };

        foreach (var entry in entries)
        {
            lines.AddRange(TextRender.WrapLines(TextRender.SanitizeHtmlToText(entry.Title), 39));
            if (entry.PublishedAt is not null)
            {
                lines.Add(entry.PublishedAt.Value.ToString("yyyy-MM-dd"));
            }
            lines.AddRange(TextRender.WrapLines(TextRender.SanitizeHtmlToText(entry.Description), 39));
            if (!string.IsNullOrWhiteSpace(entry.Link))
            {
                lines.Add(TextRender.TrimTo(entry.Link, 39));
            }
            lines.Add(string.Empty);
        }

        if (lines.Count == 0)
        {
            lines.Add("No data");
        }

        var offset = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            Cls();
            foreach (var row in lines.Skip(offset).Take(PageRows))
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
                offset = Math.Max(0, offset - PageRows);
                continue;
            }

            if (offset + PageRows < lines.Count)
            {
                offset += PageRows;
            }
        }
    }
}
