using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Bbs.Core.Content;

public static class PetsciiImageConverter
{
    private const int TargetWidth = 320;
    private const int TargetHeight = 200;
    private const int CellSize = 8;
    private const int Columns = TargetWidth / CellSize;
    private const int Rows = TargetHeight / CellSize;

    private static readonly Lazy<ulong[]> GlyphMasks = new(LoadGlyphMasks);

    public static bool IsSupportedImage(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<byte[]> ConvertFileAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        return await ConvertStreamAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<byte[]> ConvertStreamAsync(Stream source, CancellationToken cancellationToken = default)
    {
        using var image = await Image.LoadAsync<Rgba32>(source, cancellationToken).ConfigureAwait(false);
        image.Mutate(x => x
            .Resize(new ResizeOptions
            {
                Size = new Size(TargetWidth, TargetHeight),
                Mode = ResizeMode.Crop,
                Position = AnchorPositionMode.Center,
                Sampler = KnownResamplers.Lanczos3
            })
            .Grayscale());

        var frame = image.Frames.RootFrame;
        var threshold = ComputeThreshold(frame);
        var glyphMasks = GlyphMasks.Value;

        var output = new List<byte>(Rows * (Columns + 1));

        for (var cellY = 0; cellY < Rows; cellY++)
        {
            for (var cellX = 0; cellX < Columns; cellX++)
            {
                var mask = BuildBlockMask(frame, cellX * CellSize, cellY * CellSize, threshold);
                var screenCode = FindBestMatchingScreenCode(mask, glyphMasks);
                output.Add((byte)ConvertScreenCodeToPetsciiCharCode(screenCode));
            }

            output.Add(13);
        }

        return output.ToArray();
    }

    private static int FindBestMatchingScreenCode(ulong mask, ulong[] glyphMasks)
    {
        var bestCode = 32;
        var bestDistance = int.MaxValue;

        for (var i = 0; i < glyphMasks.Length; i++)
        {
            var dist = BitOperations.PopCount(mask ^ glyphMasks[i]);
            if (dist >= bestDistance)
            {
                continue;
            }

            bestDistance = dist;
            bestCode = i;
            if (dist == 0)
            {
                break;
            }
        }

        return bestCode;
    }

    private static float ComputeThreshold(ImageFrame<Rgba32> frame)
    {
        var sum = 0d;
        var count = (long)frame.Width * frame.Height;

        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                sum += Luma(frame[x, y]);
            }
        }

        var avg = (float)(sum / Math.Max(1, count));
        return Math.Clamp(avg, 32f, 224f);
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

    private static ulong[] LoadGlyphMasks()
    {
        var assembly = typeof(PetsciiImageConverter).Assembly;
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

        var darkForeground = BuildGlyphMasks(image, foregroundWhenDark: true);
        var lightForeground = BuildGlyphMasks(image, foregroundWhenDark: false);

        // For the space character (screen code 32), fewer set bits are expected.
        return BitOperations.PopCount(darkForeground[32]) <= BitOperations.PopCount(lightForeground[32])
            ? darkForeground
            : lightForeground;
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
}


