using Bbs.Petsciiator;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

var exitCode = await RunAsync(args).ConfigureAwait(false);
return exitCode;

static async Task<int> RunAsync(string[] args)
{
    var parsed = ParseArgs(args);
    if (parsed.ShowHelp || string.IsNullOrWhiteSpace(parsed.SourcePath))
    {
        PrintHelp();
        return parsed.ShowHelp ? 0 : 1;
    }

    var source = Path.GetFullPath(parsed.SourcePath);
    if (!File.Exists(source))
    {
        Console.Error.WriteLine($"Input file not found: {source}");
        return 2;
    }

    var targetDir = string.IsNullOrWhiteSpace(parsed.TargetDirectory)
        ? Path.GetDirectoryName(source)!
        : Path.GetFullPath(parsed.TargetDirectory);
    Directory.CreateDirectory(targetDir);

    var formats = parsed.Formats.Count == 0
        ? new HashSet<string>(["image", "basic"], StringComparer.OrdinalIgnoreCase)
        : parsed.Formats;

    await using var input = File.OpenRead(source);
    using var converter = new PetsciiatorConverter();
    var result = await converter.ConvertDetailedAsync(input, new PetsciiatorOptions
    {
        BbsCompatibleOutput = true,
        BbsColumns = parsed.BbsColumns,
        PreContrastPercent = parsed.ContrastPercent,
        PreColorCount = parsed.PreColorCount,
        PreDither = parsed.PreDither
    }).ConfigureAwait(false);

    var stem = Path.GetFileNameWithoutExtension(source);

    if (formats.Contains("bbs"))
    {
        var outPath = Path.Combine(targetDir, $"{stem}_bbs.seq");
        await File.WriteAllBytesAsync(outPath, result.BbsBytes).ConfigureAwait(false);
        Console.WriteLine($"bbs:   {outPath}");
    }

    if (formats.Contains("bin"))
    {
        var screenPath = Path.Combine(targetDir, $"{stem}_screen.seq");
        var colorPath = Path.Combine(targetDir, $"{stem}_color.seq");
        var bgPath = Path.Combine(targetDir, $"{stem}_bgcolor.seq");

        await File.WriteAllBytesAsync(screenPath, result.ScreenCodes.Select(v => (byte)(v & 0xFF)).ToArray()).ConfigureAwait(false);
        await File.WriteAllBytesAsync(colorPath, result.ColorRam).ConfigureAwait(false);
        await File.WriteAllBytesAsync(bgPath, [result.BackgroundColor]).ConfigureAwait(false);

        Console.WriteLine($"bin:   {screenPath}");
        Console.WriteLine($"bin:   {colorPath}");
        Console.WriteLine($"bin:   {bgPath}");
    }

    if (formats.Contains("basic"))
    {
        var outPath = Path.Combine(targetDir, $"{stem}.bas");
        await File.WriteAllLinesAsync(outPath, BuildBasic(result), System.Text.Encoding.ASCII).ConfigureAwait(false);
        Console.WriteLine($"basic: {outPath}");
    }

    if (formats.Contains("image"))
    {
        var outPath = Path.Combine(targetDir, $"{stem}_petscii.png");
        using var preview = RenderPreview(result);
        await preview.SaveAsPngAsync(outPath).ConfigureAwait(false);
        Console.WriteLine($"image: {outPath}");
    }

    if (formats.Contains("koala") || formats.Contains("hires"))
    {
        Console.WriteLine("koala/hires: not supported by Bbs.Petsciiator in this tool (yet).");
    }

    return 0;
}

static Image<Rgba32> RenderPreview(PetsciiConversionResult result)
{
    var palette = GetVic2Palette();
    using var charset = LoadCharset();
    var frame = charset.Frames.RootFrame;
    var output = new Image<Rgba32>(result.Columns * 8, result.Rows * 8, Color.Black);
    var outFrame = output.Frames.RootFrame;
    var bgColor = palette[result.BackgroundColor & 0x0F];

    for (var row = 0; row < result.Rows; row++)
    {
        for (var col = 0; col < result.Columns; col++)
        {
            var idx = (row * result.Columns) + col;
            var sc = result.ScreenCodes[idx];
            var fgColor = palette[result.ColorRam[idx] & 0x0F];
            var reverse = sc >= 128;
            var glyph = sc >= 128 ? sc - 128 : sc;

            var glyphX = (glyph % 40) * 8;
            var glyphY = (glyph / 40) * 8;

            for (var py = 0; py < 8; py++)
            {
                for (var px = 0; px < 8; px++)
                {
                    var sp = frame[glyphX + px, glyphY + py];
                    var bitOn = ((sp.R | sp.G | sp.B) & 1) == 1;
                    if (reverse)
                    {
                        bitOn = !bitOn;
                    }

                    outFrame[(col * 8) + px, (row * 8) + py] = bitOn ? fgColor : bgColor;
                }
            }
        }
    }

    return output;
}

static Rgba32[] GetVic2Palette()
{
    return
    [
        new Rgba32(0x00, 0x00, 0x00),
        new Rgba32(0xFF, 0xFF, 0xFF),
        new Rgba32(0x81, 0x33, 0x38),
        new Rgba32(0x75, 0xCE, 0xC8),
        new Rgba32(0x8E, 0x3C, 0x97),
        new Rgba32(0x56, 0xAC, 0x4D),
        new Rgba32(0x2E, 0x2C, 0x9B),
        new Rgba32(0xED, 0xF1, 0x71),
        new Rgba32(0x8E, 0x50, 0x29),
        new Rgba32(0x55, 0x38, 0x00),
        new Rgba32(0xC4, 0x6C, 0x71),
        new Rgba32(0x4A, 0x4A, 0x4A),
        new Rgba32(0x7B, 0x7B, 0x7B),
        new Rgba32(0xA9, 0xFF, 0x9F),
        new Rgba32(0x70, 0x6D, 0xEB),
        new Rgba32(0xB2, 0xB2, 0xB2)
    ];
}

static Image<Rgba32> LoadCharset()
{
    var asm = typeof(PetsciiatorConverter).Assembly;
    var resource = asm.GetManifestResourceNames()
        .FirstOrDefault(n => n.EndsWith("petscii_low.png", StringComparison.OrdinalIgnoreCase));

    if (resource is null)
    {
        throw new InvalidOperationException("Embedded charset not found.");
    }

    using var stream = asm.GetManifestResourceStream(resource)
        ?? throw new InvalidOperationException("Embedded charset stream not found.");
    return Image.Load<Rgba32>(stream);
}

static List<string> BuildBasic(PetsciiConversionResult result)
{
    var lines = new List<string>
    {
        $"60000 poke53280,{result.BackgroundColor}:poke53281,{result.BackgroundColor}:printchr$(147);",
        "60010 fori=0to999:readp,c:poke1024+i,p:poke55296+i,c:next",
        "60020 geta$:ifa$=\"\"then60020:end"
    };

    var ln = 60030;
    var dataLine = new System.Text.StringBuilder();
    var count = Math.Min(1000, result.ScreenCodes.Length);
    for (var i = 0; i < count; i++)
    {
        if (dataLine.Length == 0)
        {
            dataLine.Append(ln++).Append(" data ");
        }
        else
        {
            dataLine.Append(',');
        }

        dataLine.Append(result.ScreenCodes[i] & 0xFF).Append(',').Append(result.ColorRam[i]);
        if (dataLine.Length > 70 || i == count - 1)
        {
            lines.Add(dataLine.ToString());
            dataLine.Clear();
        }
    }

    return lines;
}

static (string? SourcePath, string? TargetDirectory, HashSet<string> Formats, int BbsColumns, float ContrastPercent, int PreColorCount, bool PreDither, bool ShowHelp) ParseArgs(string[] args)
{
    string? source = null;
    string? target = null;
    var formats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var bbsColumns = 39;
    var contrastPercent = 0f;
    var preColorCount = 0;
    var preDither = false;
    var showHelp = false;

    foreach (var raw in args)
    {
        var arg = raw?.Trim() ?? string.Empty;
        if (arg.Length == 0)
        {
            continue;
        }

        if (arg is "/?" or "-?" or "--help" or "-h")
        {
            showHelp = true;
            continue;
        }

        if (arg.StartsWith("/") || arg.StartsWith("-"))
        {
            var opt = arg[1..];
            var eq = opt.IndexOf('=');
            var key = eq >= 0 ? opt[..eq].Trim().ToLowerInvariant() : opt.Trim().ToLowerInvariant();
            var val = eq >= 0 ? opt[(eq + 1)..].Trim() : string.Empty;

            if (key == "target")
            {
                target = val;
            }
            else if (key == "format")
            {
                foreach (var part in val.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    formats.Add(part);
                }
            }
            else if (key == "bbscolumns" && int.TryParse(val, out var cols))
            {
                bbsColumns = cols;
            }
            else if (key == "contrast"
                && float.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var contrast))
            {
                contrastPercent = contrast;
            }
            else if (key == "precolors" && int.TryParse(val, out var colors))
            {
                preColorCount = colors;
            }
            else if (key == "c64x2colors")
            {
                preColorCount = 32;
            }
            else if (key == "dither")
            {
                preDither = string.IsNullOrWhiteSpace(val)
                    || !val.Equals("false", StringComparison.OrdinalIgnoreCase)
                    && !val.Equals("off", StringComparison.OrdinalIgnoreCase)
                    && !val.Equals("0", StringComparison.OrdinalIgnoreCase);
            }

            continue;
        }

        source ??= arg;
    }

    return (source, target, formats, bbsColumns, contrastPercent, preColorCount, preDither, showHelp);
}

static void PrintHelp()
{
    Console.WriteLine("Bbs.PetsciiTool");
    Console.WriteLine("Usage: bbs-petscii-tool <source-image> [/target=<dir>] [/format=image,basic,bbs,bin,koala,hires] [/bbscolumns=39] [/contrast=20] [/precolors=32] [/c64x2colors] [/dither]");
    Console.WriteLine();
    Console.WriteLine("Input supports JPG/PNG/GIF (decoded via ImageSharp).");
    Console.WriteLine("Image is scaled to 320x200 before conversion.");
    Console.WriteLine();
    Console.WriteLine("Formats:");
    Console.WriteLine("  image  -> 320x200 PNG preview");
    Console.WriteLine("  basic  -> C64 BASIC listing");
    Console.WriteLine("  bbs    -> BBS SEQ stream");
    Console.WriteLine("  bin    -> raw screen/color/bg files");
    Console.WriteLine("  koala  -> currently not supported");
    Console.WriteLine("  hires  -> currently not supported");
    Console.WriteLine();
    Console.WriteLine("Preprocessing:");
    Console.WriteLine("  contrast=<percent>   -> optional contrast before conversion (e.g. 20)");
    Console.WriteLine("  precolors=<n>        -> optional color count reduction before conversion (2..256)");
    Console.WriteLine("  c64x2colors          -> shortcut for precolors=32");
    Console.WriteLine("  dither               -> optional Floyd-Steinberg dithering (used with precolors)");
}
