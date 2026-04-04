using Bbs.Core;
using Bbs.Core.Content;
using Bbs.Terminals;
using Bbs.Tenants.Content;

namespace Bbs.Tenants;

public sealed class PetsciiArtGallery : PetsciiThread
{
    private readonly IPetsciiGalleryService _gallery = new PetsciiGalleryService();
    private static string ResolveGalleryRoot()
    {
        var env = Environment.GetEnvironmentVariable("PETSCII_GALLERY_ROOT")?.Trim();
        if (!string.IsNullOrWhiteSpace(env))
        {
            return Path.GetFullPath(env);
        }

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "petscii-art-gallery"),
            Path.Combine(Directory.GetCurrentDirectory(), "petscii-art-gallery"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "petscii-art-gallery"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "petscii-art-gallery")
        };

        foreach (var candidate in candidates)
        {
            var full = Path.GetFullPath(candidate);
            if (Directory.Exists(full))
            {
                return full;
            }
        }

        // Last-resort default (also shown in docs).
        return Path.Combine(AppContext.BaseDirectory, "petscii-art-gallery");
    }

    private static string GalleryRoot => ResolveGalleryRoot();

    public override async Task DoLoopAsync(CancellationToken cancellationToken = default)
    {
        var randomize = false;
        var slideshow = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            await NormalizeTextModeAsync(cancellationToken).ConfigureAwait(false);
            var authors = await _gallery.ListAuthorsAsync(GalleryRoot, cancellationToken).ConfigureAwait(false);

            Cls();
            Println("PETSCII Art Gallery");
            Println(new string('-', 39));
            Println($"R) Randomize: {(randomize ? "ON" : "OFF")}");
            Println($"S) Slideshow: {(slideshow ? "ON" : "OFF")}");
            Println();

            var top = Math.Min(9, authors.Count);
            for (var i = 0; i < top; i++)
            {
                var name = Path.GetFileName(authors[i]);
                Println($"{i + 1}) {name}");
            }

            if (authors.Count == 0)
            {
                Println("No gallery files found.");
                Println($"Path: {TextRender.TrimTo(GalleryRoot, 39)}");
                Println("Set PETSCII_GALLERY_ROOT env var.");
            }

            Println(".) Back");
            Print("Choice: ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);

            var input = (await ReadLineAsync(maxLength: 8, cancellationToken: cancellationToken).ConfigureAwait(false)).Trim().ToUpperInvariant();
            if (input == ".")
            {
                return;
            }

            if (input == "R")
            {
                randomize = !randomize;
                continue;
            }

            if (input == "S")
            {
                slideshow = !slideshow;
                continue;
            }

            if (!int.TryParse(input, out var idx) || idx < 1 || idx > top)
            {
                continue;
            }

            await ShowAuthorAsync(authors[idx - 1], randomize, slideshow, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ShowAuthorAsync(string authorPath, bool randomize, bool slideshow, CancellationToken cancellationToken)
    {
        var drawings = (await _gallery.ListDrawingsAsync(authorPath, cancellationToken).ConfigureAwait(false)).ToList();
        if (drawings.Count == 0)
        {
            return;
        }

        if (randomize)
        {
            var rnd = new Random();
            drawings = drawings.OrderBy(_ => rnd.Next()).ToList();
        }

        var i = 0;
        while (!cancellationToken.IsCancellationRequested && i < drawings.Count)
        {
            var file = drawings[i];
            byte[] data;
            try
            {
                // Check if it's Petmate JSON format
                if (Bbs.Core.Content.PetmateService.IsPetmateJson(file))
                {
                    var jsonContent = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                    data = Bbs.Core.Content.PetmateService.RenderPetmateJson(jsonContent);
                }
                else
                {
                    data = await _gallery.ReadDrawingAsync(file, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error rendering {file}: {ex.Message}");
                i++;
                continue;
            }

            Cls();
            Write(data);
            // Artwork streams may leave reverse/case/color state changed.
            // Normalize before printing interactive prompt text.
            await NormalizeTextModeAsync(cancellationToken).ConfigureAwait(false);
            Println();
            Println($"[{i + 1}/{drawings.Count}] {TextRender.TrimTo(Path.GetFileName(file), 30)}");
            Print("N=Next  -=Prev  .=Back");
            await FlushAsync(cancellationToken).ConfigureAwait(false);

            if (slideshow)
            {
                var key = await KeyPressedAsync(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
                if (key == '.')
                {
                    return;
                }

                if (key == '-')
                {
                    i = Math.Max(0, i - 1);
                }
                else
                {
                    i++;
                }
            }
            else
            {
                var input = (await ReadLineAsync(maxLength: 2, cancellationToken: cancellationToken).ConfigureAwait(false)).Trim();
                if (input == ".")
                {
                    return;
                }

                if (input == "-")
                {
                    i = Math.Max(0, i - 1);
                }
                else
                {
                    i++;
                }
            }
        }
    }

    private async Task NormalizeTextModeAsync(CancellationToken cancellationToken)
    {
        // SyncTERM-compatible reset after raw SEQ playback:
        // reverse off, white, force lowercase/uppercase charset.
        Write(
            PetsciiKeys.ReverseOff,
            PetsciiKeys.White,
            PetsciiKeys.Lowercase);
        await FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}





