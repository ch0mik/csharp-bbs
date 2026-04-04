using System.Numerics;
using System.Net.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace Bbs.Petsciiator;

public sealed class PetsciiatorConverter : IDisposable
{
    private const int CellSize = 8;
    private static readonly int[] Vic2Palette =
    [
        0x000000, // 0 black
        0xFFFFFF, // 1 white
        0x813338, // 2 red
        0x75CEC8, // 3 cyan
        0x8E3C97, // 4 purple
        0x56AC4D, // 5 green
        0x2E2C9B, // 6 blue
        0xEDF171, // 7 yellow
        0x8E5029, // 8 orange
        0x553800, // 9 brown
        0xC46C71, // 10 light red
        0x4A4A4A, // 11 dark gray
        0x7B7B7B, // 12 gray
        0xA9FF9F, // 13 light green
        0x706DEB, // 14 light blue
        0xB2B2B2  // 15 light gray
    ];

    private static readonly byte[] BbsColorCodes =
    [
        144, // black
        5,   // white
        28,  // red
        159, // cyan
        156, // purple
        30,  // green
        31,  // blue
        158, // yellow
        129, // orange
        149, // brown
        150, // light red
        151, // dark gray
        152, // gray
        153, // light green
        154, // light blue
        155  // light gray
    ];

    private static readonly Lazy<ulong[]> GlyphMasks = new(LoadGlyphMasks);
    private static readonly Lazy<float[][]> GlyphSignatures = new(LoadGlyphSignatures);

    private readonly HttpClient? _httpClient;
    private readonly bool _ownsHttpClient;

    public PetsciiatorConverter(HttpClient? httpClient = null)
    {
        if (httpClient is null)
        {
            _httpClient = new HttpClient();
            _ownsHttpClient = true;
        }
        else
        {
            _httpClient = httpClient;
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient?.Dispose();
        }
    }

    public Task<byte[]> ConvertAsync(byte[] imageBytes, PetsciiatorOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        return ConvertAsync(new MemoryStream(imageBytes, writable: false), options, cancellationToken);
    }

    public async Task<byte[]> ConvertAsync(Stream imageStream, PetsciiatorOptions? options = null, CancellationToken cancellationToken = default)
    {
        var result = await ConvertDetailedAsync(imageStream, options, cancellationToken).ConfigureAwait(false);
        return (options ?? PetsciiatorOptions.Default).BbsCompatibleOutput ? result.BbsBytes : result.RawBytes;
    }

    public Task<PetsciiConversionResult> ConvertDetailedAsync(byte[] imageBytes, PetsciiatorOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        return ConvertDetailedAsync(new MemoryStream(imageBytes, writable: false), options, cancellationToken);
    }

    public async Task<PetsciiConversionResult> ConvertDetailedAsync(Stream imageStream, PetsciiatorOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageStream);

        var settings = options ?? PetsciiatorOptions.Default;
        ValidateOptions(settings);

        using var image = await Image.LoadAsync<Rgba32>(imageStream, cancellationToken).ConfigureAwait(false);
        image.Mutate(ctx =>
        {
            ctx.Resize(CreateResizeOptions(settings));
            if (MathF.Abs(settings.PreContrastPercent) > float.Epsilon)
            {
                var contrast = 1f + (settings.PreContrastPercent / 100f);
                ctx.Contrast(Math.Clamp(contrast, 0.01f, 4f));
            }

            if (settings.PreColorCount > 0)
            {
                ctx.Quantize(new WuQuantizer(new QuantizerOptions
                {
                    MaxColors = Math.Clamp(settings.PreColorCount, 2, 256),
                    Dither = settings.PreDither ? KnownDitherings.FloydSteinberg : null
                }));
            }
        });

        var frame = image.Frames.RootFrame;
        var glyphSignatures = GlyphSignatures.Value;

        var columns = settings.TargetWidth / CellSize;
        var rows = settings.TargetHeight / CellSize;
        var screenCodes = new int[rows * columns];
        var colorRam = new byte[rows * columns];
        var colorIndices = BuildPaletteIndexMap(frame);
        var background = FindBackgroundColor(frame);

        for (var cellY = 0; cellY < rows; cellY++)
        {
            for (var cellX = 0; cellX < columns; cellX++)
            {
                var index = (cellY * columns) + cellX;
                var xStart = cellX * CellSize;
                var yStart = cellY * CellSize;

                colorRam[index] = FindCellForegroundColor(colorIndices, frame.Width, xStart, yStart, background);
                var signature = BuildCellSignature(colorIndices, frame.Width, xStart, yStart, background);
                screenCodes[index] = FindBestMatchingScreenCode(signature, glyphSignatures, settings.PreferLightForeground);
            }
        }

        var raw = BuildRawOutput(screenCodes, columns, rows);
        var bbs = BuildBbsOutput(screenCodes, colorRam, columns, rows, settings.BbsColumns);

        return new PetsciiConversionResult(
            columns,
            rows,
            screenCodes,
            colorRam,
            background,
            raw,
            bbs);
    }

    public async Task<byte[]> ConvertFromUrlAsync(string url, PetsciiatorOptions? options = null, CancellationToken cancellationToken = default)
    {
        var uri = NormalizeUrl(url);
        await using var stream = await _httpClient!.GetStreamAsync(uri, cancellationToken).ConfigureAwait(false);
        return await ConvertAsync(stream, options, cancellationToken).ConfigureAwait(false);
    }

    public Task<byte[]> ConvertFromUrlAsync(Uri url, PetsciiatorOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (!url.IsAbsoluteUri)
        {
            throw new ArgumentException("The URL must be absolute.", nameof(url));
        }

        return ConvertFromUrlAsync(url.ToString(), options, cancellationToken);
    }

    private static void ValidateOptions(PetsciiatorOptions options)
    {
        if (options.TargetWidth <= 0 || options.TargetWidth % CellSize != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.TargetWidth), "Target width must be a positive multiple of 8.");
        }

        if (options.TargetHeight <= 0 || options.TargetHeight % CellSize != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.TargetHeight), "Target height must be a positive multiple of 8.");
        }

        if (options.BbsColumns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.BbsColumns), "BBS columns must be greater than zero.");
        }

        if (options.PreContrastPercent is < -99f or > 300f)
        {
            throw new ArgumentOutOfRangeException(nameof(options.PreContrastPercent), "Pre-contrast must be between -99 and 300 percent.");
        }

        if (options.PreColorCount is < 0 or > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(options.PreColorCount), "Pre-color count must be in range 0..256.");
        }
    }

    private static ResizeOptions CreateResizeOptions(PetsciiatorOptions options)
    {
        var mode = options.ResizeMode switch
        {
            PetsciiResizeMode.Crop => ResizeMode.Crop,
            PetsciiResizeMode.Pad => ResizeMode.Pad,
            PetsciiResizeMode.Stretch => ResizeMode.Stretch,
            _ => ResizeMode.Crop
        };

        return new ResizeOptions
        {
            Size = new Size(options.TargetWidth, options.TargetHeight),
            Mode = mode,
            Position = AnchorPositionMode.Center,
            Sampler = KnownResamplers.Lanczos3,
            PadColor = Color.Black
        };
    }

    private static int FindBestMatchingScreenCode(float[] cellSignature, float[][] glyphSignatures, bool preferLightForeground)
    {
        var bestCode = 32;
        var bestDistance = double.MaxValue;

        for (var i = 0; i < glyphSignatures.Length; i++)
        {
            var glyphSignature = glyphSignatures[i];
            var d1 = glyphSignature[0] - cellSignature[0];
            var d2 = glyphSignature[1] - cellSignature[1];
            var d3 = glyphSignature[2] - cellSignature[2];
            var d4 = glyphSignature[3] - cellSignature[3];
            var d5 = glyphSignature[4] - cellSignature[4];
            var d6 = glyphSignature[5] - cellSignature[5];
            var d7 = glyphSignature[6] - cellSignature[6];
            var d8 = glyphSignature[7] - cellSignature[7];
            var d9 = glyphSignature[8] - cellSignature[8];
            var d10 = glyphSignature[9] - cellSignature[9];

            var dist = Math.Sqrt(
                (d1 * d1) + (d2 * d2) + (d3 * d3) + (d4 * d4) + (d5 * d5) +
                (d6 * d6) + (d7 * d7) + (d8 * d8) + (d9 * d9) + (d10 * d10));

            if (preferLightForeground && ((i >= 64 && i <= 127) || (i >= 192 && i <= 255)))
            {
                dist *= 0.95d;
            }

            if (dist >= bestDistance)
            {
                continue;
            }

            bestDistance = dist;
            bestCode = i;

            if (dist <= 0.00001d)
            {
                break;
            }
        }

        return bestCode;
    }

    private static byte FindBackgroundColor(ImageFrame<Rgba32> frame)
    {
        Span<int> counts = stackalloc int[16];
        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                var idx = GetNearestVic2ColorIndex(frame[x, y]);
                counts[idx]++;
            }
        }

        var maxCount = -1;
        var best = 0;
        for (var i = 0; i < counts.Length; i++)
        {
            if (counts[i] <= maxCount)
            {
                continue;
            }

            maxCount = counts[i];
            best = i;
        }

        return (byte)best;
    }

    private static byte[] BuildPaletteIndexMap(ImageFrame<Rgba32> frame)
    {
        var indices = new byte[frame.Width * frame.Height];
        var pos = 0;
        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                indices[pos++] = (byte)GetNearestVic2ColorIndex(frame[x, y]);
            }
        }

        return indices;
    }

    private static byte FindCellForegroundColor(byte[] colorIndices, int width, int xStart, int yStart, byte background)
    {
        Span<int> counts = stackalloc int[16];
        for (var y = 0; y < CellSize; y++)
        {
            for (var x = 0; x < CellSize; x++)
            {
                var idx = colorIndices[(yStart + y) * width + (xStart + x)];
                counts[idx]++;
            }
        }

        var bg = background & 0x0F;
        counts[bg] = 0;

        var maxCount = 0;
        var best = bg;
        for (var i = 0; i < counts.Length; i++)
        {
            if (counts[i] <= maxCount)
            {
                continue;
            }

            maxCount = counts[i];
            best = i;
        }

        return (byte)best;
    }

    private static float[] BuildCellSignature(byte[] colorIndices, int width, int xStart, int yStart, byte background)
    {
        var signature = new float[10];
        for (var y = 0; y < CellSize; y++)
        {
            for (var x = 0; x < CellSize; x++)
            {
                var idx = colorIndices[(yStart + y) * width + (xStart + x)];
                if (idx == background)
                {
                    continue;
                }

                AccumulateSignature(signature, x, y);
            }
        }

        return signature;
    }

    private static void AccumulateSignature(float[] signature, int x, int y)
    {
        if (x < 4 && y < 4) signature[0]++;
        if (x > 3 && y < 4) signature[1]++;
        if (x < 4 && y > 3) signature[2]++;
        if (x > 3 && y > 3) signature[3]++;
        if (x > 2 && x < 7 && y > 2 && y < 7) signature[4]++;
        if (x == y || (7 - y) == x) signature[5]++;
        if (x == 0) signature[6]++;
        if (x == 7) signature[7]++;
        if (y == 0) signature[8]++;
        if (y == 7) signature[9]++;
    }

    private static int GetNearestVic2ColorIndex(Rgba32 px)
    {
        var best = 0;
        var bestDistance = long.MaxValue;
        var r = px.R;
        var g = px.G;
        var b = px.B;

        for (var i = 0; i < Vic2Palette.Length; i++)
        {
            var color = Vic2Palette[i];
            var cr = (color >> 16) & 0xFF;
            var cg = (color >> 8) & 0xFF;
            var cb = color & 0xFF;
            var dr = r - cr;
            var dg = g - cg;
            var db = b - cb;
            var dist = (long)(dr * dr) + (long)(dg * dg) + (long)(db * db);
            if (dist >= bestDistance)
            {
                continue;
            }

            bestDistance = dist;
            best = i;
        }

        return best;
    }

    private static ulong BuildBlockMask(ImageFrame<Rgba32> frame, int xStart, int yStart, float threshold)
    {
        ulong mask = 0;
        var bit = 0;

        for (var y = 0; y < CellSize; y++)
        {
            for (var x = 0; x < CellSize; x++)
            {
                if (Luma(frame[xStart + x, yStart + y]) < threshold)
                {
                    mask |= 1UL << bit;
                }

                bit++;
            }
        }

        return mask;
    }

    private static float Luma(Rgba32 px)
    {
        return (0.299f * px.R) + (0.587f * px.G) + (0.114f * px.B);
    }

    private static float[][] LoadGlyphSignatures()
    {
        var masks = GlyphMasks.Value;
        var signatures = new float[masks.Length][];
        for (var i = 0; i < masks.Length; i++)
        {
            signatures[i] = BuildGlyphSignature(masks[i]);
        }

        return signatures;
    }

    private static float[] BuildGlyphSignature(ulong mask)
    {
        var signature = new float[10];
        for (var bit = 0; bit < 64; bit++)
        {
            if (((mask >> bit) & 1UL) == 0)
            {
                continue;
            }

            var x = bit % 8;
            var y = bit / 8;
            AccumulateSignature(signature, x, y);
        }

        return signature;
    }

    private static ulong[] LoadGlyphMasks()
    {
        var assembly = typeof(PetsciiatorConverter).Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("petscii_low.png", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            throw new InvalidOperationException("Embedded PETSCII charset resource not found.");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Cannot open embedded PETSCII charset stream.");
        using var image = Image.Load<Rgba32>(stream);

        // Match Java petsciiator logic: glyph "on" pixels are the light ones (pc & 1 == 1).
        return BuildGlyphMasks(image, foregroundWhenDark: false);
    }

    private static ulong[] BuildGlyphMasks(Image<Rgba32> image, bool foregroundWhenDark)
    {
        if (image.Width < 320 || image.Height < 56)
        {
            throw new InvalidOperationException("Unexpected PETSCII charset image dimensions.");
        }

        var frame = image.Frames.RootFrame;
        var masks = new ulong[256];

        var idx = 0;
        for (var y = 0; y < 56 && idx < 256; y += 8)
        {
            for (var x = 0; x < 320 && idx < 256; x += 8)
            {
                masks[idx++] = BuildGlyphMask(frame, x, y, foregroundWhenDark);
            }
        }

        if (idx < 256)
        {
            throw new InvalidOperationException("Failed to read all PETSCII glyphs from charset image.");
        }

        return masks;
    }

    private static ulong BuildGlyphMask(ImageFrame<Rgba32> frame, int xStart, int yStart, bool foregroundWhenDark)
    {
        ulong mask = 0;
        var bit = 0;

        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                var isDark = Luma(frame[xStart + x, yStart + y]) < 128f;
                var isForeground = foregroundWhenDark ? isDark : !isDark;
                if (isForeground)
                {
                    mask |= 1UL << bit;
                }

                bit++;
            }
        }

        return mask;
    }

    private static int ConvertScreenCodeToPetsciiCharCode(int screenCode)
    {
        if (screenCode == 94)
        {
            return 255;
        }

        if (screenCode >= 128)
        {
            return ConvertScreenCodeToPetsciiCharCode(screenCode - 128);
        }

        if (screenCode >= 32 && screenCode <= 63)
        {
            return screenCode;
        }

        if (screenCode <= 31)
        {
            return screenCode + 64;
        }

        if (screenCode >= 64 && screenCode <= 95)
        {
            return screenCode + 32;
        }

        if (screenCode >= 96 && screenCode <= 127)
        {
            return screenCode + 64;
        }

        return 32;
    }

    private static byte[] BuildRawOutput(int[] screenCodes, int columns, int rows)
    {
        var output = new List<byte>(rows * (columns + 1));
        for (var row = 0; row < rows; row++)
        {
            var rowOffset = row * columns;
            for (var col = 0; col < columns; col++)
            {
                output.Add((byte)ConvertScreenCodeToPetsciiCharCode(screenCodes[rowOffset + col]));
            }

            output.Add(13);
        }

        return output.ToArray();
    }

    private static byte[] BuildBbsOutput(int[] screenCodes, byte[] colorRam, int columns, int rows, int requestedColumns)
    {
        var displayColumns = Math.Clamp(requestedColumns, 1, columns);
        var output = new List<byte>(4 + (rows * (displayColumns + 4)))
        {
            147, // CLR/HOME
            14, // switch to lowercase/uppercase charset (matches petscii_low.png)
        };

        for (var row = 0; row < rows; row++)
        {
            var rowOffset = row * columns;
            var reverse = false;
            var lastColor = -1;

            for (var col = 0; col < displayColumns; col++)
            {
                var screenCode = screenCodes[rowOffset + col];
                var color = colorRam[rowOffset + col] & 0x0F;
                var nextReverse = screenCode >= 128;

                if (color != lastColor)
                {
                    if (reverse)
                    {
                        output.Add(146); // reverse off before color switch
                        reverse = false;
                    }

                    output.Add(BbsColorCodes[color]);
                    lastColor = color;
                }

                if (nextReverse && !reverse)
                {
                    output.Add(18); // reverse on
                    reverse = true;
                }
                else if (!nextReverse && reverse)
                {
                    output.Add(146); // reverse off
                    reverse = false;
                }

                output.Add((byte)ConvertScreenCodeToPetsciiCharCode(screenCode));
            }

            if (reverse)
            {
                output.Add(146); // reverse off before next line
            }

            output.Add(13); // newline
        }

        output.Add(142); // switch back to uppercase/graphics charset
        return output.ToArray();
    }

    private static Uri NormalizeUrl(string url)
    {
        var value = (url ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("The URL cannot be empty.", nameof(url));
        }

        if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            value = "https://" + value;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("The URL is invalid.", nameof(url));
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only HTTP and HTTPS URLs are supported.", nameof(url));
        }

        return uri;
    }
}


