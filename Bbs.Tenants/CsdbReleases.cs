using Bbs.Core.Content;
using Bbs.Core.Protocols;
using Bbs.Tenants.Content;
using Bbs.Terminals;

namespace Bbs.Tenants;

public sealed class CsdbReleases : PetsciiThread
{
    private readonly ICsdbService _csdb;
    private readonly IXModemSender _xmodem;
    private readonly bool _enableXmodemDownloads;

    public CsdbReleases() : this(enableXmodemDownloads: false, csdb: null, xmodem: null)
    {
    }

    public CsdbReleases(bool enableXmodemDownloads, ICsdbService? csdb = null, IXModemSender? xmodem = null)
    {
        _enableXmodemDownloads = enableXmodemDownloads;
        _csdb = csdb ?? new Bbs.Tenants.Content.CsdbService();
        _xmodem = xmodem ?? new XModemSender();
    }

    public override async Task DoLoopAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Cls();
            Println(_enableXmodemDownloads ? "CSDb Releases (SD2IEC)" : "CSDb Releases");
            Println(new string('-', 39));
            Println("1) Latest releases");
            Println("2) Search");
            Println(".) Back");
            Print("Choice: ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);

            var input = (await ReadLineAsync(maxLength: 16, cancellationToken: cancellationToken).ConfigureAwait(false)).Trim();
            if (input == ".")
            {
                return;
            }

            if (input == "1")
            {
                await ShowLatestAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (input == "2")
            {
                await SearchAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ShowLatestAsync(CancellationToken cancellationToken)
    {
        try
        {
            var items = await _csdb.GetLatestReleasesAsync(cancellationToken).ConfigureAwait(false);
            await ShowReleaseListAsync(items, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SearchAsync(CancellationToken cancellationToken)
    {
        Cls();
        Print("Search query: ");
        await FlushAsync(cancellationToken).ConfigureAwait(false);
        var query = (await ReadLineAsync(maxLength: 64, cancellationToken: cancellationToken).ConfigureAwait(false)).Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        try
        {
            var items = await _csdb.SearchReleasesAsync(query, cancellationToken).ConfigureAwait(false);
            await ShowReleaseListAsync(items, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ShowReleaseListAsync(IReadOnlyList<CsdbReleaseItem> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            Cls();
            Println("No results.");
            Print("Press ENTER...");
            await FlushAsync(cancellationToken).ConfigureAwait(false);
            await ReadLineAsync(maxLength: 1, cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        var page = 0;
        const int pageSize = 8;

        while (!cancellationToken.IsCancellationRequested)
        {
            Cls();
            Println("CSDb results");
            Println(new string('-', 39));

            var start = page * pageSize;
            var rows = items.Skip(start).Take(pageSize).ToArray();
            for (var i = 0; i < rows.Length; i++)
            {
                var n = i + 1;
                var title = TextRender.TrimTo(TextRender.SanitizeHtmlToText(rows[i].Title), 32);
                Println($"{n}) {title}");
            }

            Println();
            Println(_enableXmodemDownloads ? "# open, D# download" : "# open");
            Print("N+/N-, . back > ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);
            var input = (await ReadLineAsync(maxLength: 8, cancellationToken: cancellationToken).ConfigureAwait(false)).Trim();

            if (input == ".")
            {
                return;
            }

            if (string.Equals(input, "n+", StringComparison.OrdinalIgnoreCase) || string.Equals(input, "+", StringComparison.OrdinalIgnoreCase) || string.Equals(input, "n", StringComparison.OrdinalIgnoreCase))
            {
                if ((page + 1) * pageSize < items.Count)
                {
                    page++;
                }
                continue;
            }

            if ((string.Equals(input, "n-", StringComparison.OrdinalIgnoreCase) || string.Equals(input, "-", StringComparison.OrdinalIgnoreCase)) && page > 0)
            {
                page--;
                continue;
            }

            if (_enableXmodemDownloads && TryParseDownloadSelection(input, rows, out var downloadItem))
            {
                await DownloadReleaseAsync(downloadItem, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (!int.TryParse(input, out var idx) || idx < 1 || idx > rows.Length)
            {
                continue;
            }

            await ShowReleaseAsync(rows[idx - 1], cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool TryParseDownloadSelection(string input, IReadOnlyList<CsdbReleaseItem> rows, out CsdbReleaseItem item)
    {
        item = default!;
        if (string.IsNullOrWhiteSpace(input) || input.Length < 2)
        {
            return false;
        }

        if (!input.StartsWith("d", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!int.TryParse(input[1..], out var idx) || idx < 1 || idx > rows.Count)
        {
            return false;
        }

        item = rows[idx - 1];
        return true;
    }

    private async Task ShowReleaseAsync(CsdbReleaseItem item, CancellationToken cancellationToken)
    {
        Cls();
        Println(TextRender.TrimTo(item.Title, 39));
        Println(new string('-', 39));
        if (item.PublishedAt is not null)
        {
            Println(item.PublishedAt.Value.ToString("yyyy-MM-dd"));
        }

        if (!string.IsNullOrWhiteSpace(item.ReleasedBy))
        {
            Println("From: " + TextRender.TrimTo(item.ReleasedBy, 33));
        }

        if (!string.IsNullOrWhiteSpace(item.Type))
        {
            Println("Type: " + TextRender.TrimTo(item.Type, 33));
        }

        Println("ID: " + item.Id);
        Println();
        Println(TextRender.TrimTo(item.ReleaseUri, 39));
        if (!string.IsNullOrWhiteSpace(item.DownloadLink))
        {
            Println(TextRender.TrimTo(item.DownloadLink, 39));
        }

        Println();
        if (_enableXmodemDownloads)
        {
            Print("D=download, ENTER=back > ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);
            var cmd = (await ReadLineAsync(maxLength: 8, cancellationToken: cancellationToken).ConfigureAwait(false)).Trim();
            if (string.Equals(cmd, "d", StringComparison.OrdinalIgnoreCase))
            {
                await DownloadReleaseAsync(item, cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        Print("Press ENTER...");
        await FlushAsync(cancellationToken).ConfigureAwait(false);
        await ReadLineAsync(maxLength: 1, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task DownloadReleaseAsync(CsdbReleaseItem item, CancellationToken cancellationToken)
    {
        try
        {
            Cls();
            Println("Resolving release file...");
            await FlushAsync(cancellationToken).ConfigureAwait(false);

            var payload = await _csdb.DownloadReleaseAsync(item, cancellationToken).ConfigureAwait(false);
            if (payload is null)
            {
                Println("No downloadable file found.");
                Print("Press ENTER...");
                await FlushAsync(cancellationToken).ConfigureAwait(false);
                await ReadLineAsync(maxLength: 1, cancellationToken: cancellationToken).ConfigureAwait(false);
                return;
            }

            Cls();
            Println("XMODEM transfer");
            Println(new string('-', 39));
            Println(TextRender.TrimTo(payload.FileName, 39));
            Println($"Size: {payload.Content.Length} bytes");
            Println("Prepare receiver on C64 now.");
            Print("ENTER=start, .=cancel > ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);

            var answer = (await ReadLineAsync(maxLength: 2, cancellationToken: cancellationToken).ConfigureAwait(false)).Trim();
            if (answer == ".")
            {
                return;
            }

            var result = await _xmodem.SendAsync(Io, payload.Content, payload.FileName, cancellationToken).ConfigureAwait(false);

            Cls();
            Println(result.Success ? "Transfer complete." : "Transfer failed.");
            Println(TextRender.TrimTo(result.Message, 39));
            Println($"Blocks: {result.BlocksSent}, bytes: {result.BytesSent}");
            Print("Press ENTER...");
            await FlushAsync(cancellationToken).ConfigureAwait(false);
            await ReadLineAsync(maxLength: 1, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ShowErrorAsync(Exception ex, CancellationToken cancellationToken)
    {
        Cls();
        Println("CSDb error:");
        Println(TextRender.TrimTo(ex.Message, 39));
        Println();
        Print("Press ENTER...");
        await FlushAsync(cancellationToken).ConfigureAwait(false);
        await ReadLineAsync(maxLength: 1, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
