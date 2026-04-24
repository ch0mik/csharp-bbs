using Bbs.Terminals;
using Tenant = Bbs.Tenants;

namespace Bbs.Server;

public sealed class StdChoice : PetsciiThread
{
    private const string SessionInlineImagesKey = "session:inline-petscii-images";
    private static readonly Lazy<byte[]?> HeaderSeq = new(LoadHeaderSeq);

    public override async Task DoLoopAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Cls();
            PrintEightBitzHeader();
            Println();
            Println("1) Art Gallery");
            Println("2) RSS");
            Println("3) Wikipedia");
            Println("4) CSDB");
            Println("5) ZorkMachine");
            Println("6) CommodoreNews");
            Println("7) Quiz");
            Println("B) 8-Bitz blog (polish)");
            Println($"I) Inline IMG: {(IsSessionInlineImagesEnabled() ? "ON" : "OFF")}");
            Println("Q) Quit");
            Println();
            Print("Choice: ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);

            var rawInput = (await ReadLineAsync(maxLength: 16, cancellationToken: cancellationToken).ConfigureAwait(false)).Trim();
            var choice = NormalizeChoice(rawInput);

            if (choice is "Q" or "QUIT" or "X")
            {
                Println();
                Println("Bye!");
                await FlushAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            if (choice is "1" or "GALLERY" or "PETSCII" or "PETSCIIARTGALLERY")
            {
                await LaunchAsync(new Tenant.PetsciiArtGallery(), cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (choice is "2" or "RSS")
            {
                await LaunchAsync(new Tenant.RssPetscii(), cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (choice is "3" or "WIKI" or "WIKIPEDIA")
            {
                await LaunchAsync(new Tenant.WikipediaPetscii(), cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (choice is "4" or "CSDB")
            {
                await ShowCsdbMenuAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (choice is "5" or "ZORK")
            {
                await LaunchAsync(new Tenant.ZorkMachine(), cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (choice is "6" or "COMMODORE" or "COMMODORENEWS" or "NEWS")
            {
                await LaunchAsync(new Tenant.CommodoreNews(), cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (choice is "7" or "QUIZ" or "QUIZPETSCII" or "MILLIONAIRE" or "MILIONERZY")
            {
                await LaunchAsync(new Tenant.QuizPetscii(), cancellationToken).ConfigureAwait(false);
                continue;
            }
            if (choice is "B" or "8BITZ" or "8-BITZ" or "EIGHTBITZ")
            {
                await LaunchAsync(new Tenant.EightBitz(), cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (choice is "I" or "IMG" or "IMAGES")
            {
                ToggleSessionInlineImages();
                Cls();
                PrintEightBitzHeader();
                Println();
                Println($"Inline images: {(IsSessionInlineImagesEnabled() ? "ON" : "OFF")} (session)");
                Println();
                Println("Press ENTER...");
                await FlushAsync(cancellationToken).ConfigureAwait(false);
                await ReadLineAsync(maxLength: 1, cancellationToken: cancellationToken).ConfigureAwait(false);
                continue;
            }

            Console.WriteLine($"{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} WARN Unknown StdChoice input '{rawInput}' normalized='{choice}', client={ClientId}");
            Println();
            Println("Unknown option.");
            Println("Press ENTER...");
            await FlushAsync(cancellationToken).ConfigureAwait(false);
            await ReadLineAsync(maxLength: 1, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ShowCsdbMenuAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Cls();
            PrintEightBitzHeader();
            Println("CSDB");
            Println();
            Println("1) Releases");
            Println("2) SD2IEC");
            Println(".) Back");
            Println();
            Print("Choice: ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);

            var input = (await ReadLineAsync(maxLength: 8, cancellationToken: cancellationToken).ConfigureAwait(false)).Trim();
            var choice = NormalizeChoice(input);

            if (choice is "." or "BACK" or "Q" or "QUIT")
            {
                return;
            }

            if (choice is "1" or "RELEASES")
            {
                await LaunchAsync(new Tenant.CsdbReleases(), cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (choice is "2" or "SD2IEC")
            {
                await LaunchAsync(new Tenant.CsdbReleasesSD2IEC(), cancellationToken).ConfigureAwait(false);
                continue;
            }

            Println();
            Println("Unknown option.");
            Println("Press ENTER...");
            await FlushAsync(cancellationToken).ConfigureAwait(false);
            await ReadLineAsync(maxLength: 1, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private void PrintEightBitzHeader()
    {
        var seq = HeaderSeq.Value;
        if (seq is { Length: > 0 })
        {
            Write(seq);
            Write(PetsciiKeys.White);
            return;
        }

        PrintBlockLine(PetsciiKeys.Blue);
        PrintColorLine(PetsciiKeys.LightBlue, "   +-------------------------------+");
        PrintColorLine(PetsciiKeys.Cyan, "   |         * 8-BITZ *           |");
        PrintColorLine(PetsciiKeys.LightGreen, "   |       RETRO PETSCII BBS      |");
        PrintColorLine(PetsciiKeys.Yellow, "   |   RSS WIKI CSDB ZORK ART     |");
        PrintColorLine(PetsciiKeys.Purple, "   |   NEWS GALLERY BLOG QUIZ     |");
        PrintColorLine(PetsciiKeys.LightBlue, "   +-------------------------------+");
        PrintBlockLine(PetsciiKeys.Blue);
        Write(PetsciiKeys.White);
    }

    private void PrintBlockLine(int color)
    {
        Write(color);
        for (var i = 0; i < 39; i++)
        {
            Write(160);
        }

        Println();
    }

    private void PrintColorLine(int color, string text)
    {
        Write(color);
        Println(text);
    }

    private static string NormalizeChoice(string value)
    {
        var trimmed = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        if (trimmed.Length > 1 && char.IsDigit(trimmed[0]))
        {
            return trimmed[0].ToString();
        }

        return trimmed;
    }

    private bool IsSessionInlineImagesEnabled()
    {
        var value = GetCustomObject(SessionInlineImagesKey);
        return value is not bool b || b;
    }

    private void ToggleSessionInlineImages()
    {
        SetCustomObject(SessionInlineImagesKey, !IsSessionInlineImagesEnabled());
    }

    private static byte[]? LoadHeaderSeq()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", "stdchoice_header.seq"),
            Path.Combine(AppContext.BaseDirectory, "stdchoice_header.seq"),
            Path.Combine(Directory.GetCurrentDirectory(), "Bbs.Server", "Assets", "stdchoice_header.seq"),
            Path.Combine(Directory.GetCurrentDirectory(), "Assets", "stdchoice_header.seq")
        };

        foreach (var path in candidates)
        {
            try
            {
                if (File.Exists(path))
                {
                    var bytes = File.ReadAllBytes(path);
                    if (bytes.Length > 0)
                    {
                        return bytes;
                    }
                }
            }
            catch
            {
                // ignore and try next candidate
            }
        }

        return null;
    }
}

