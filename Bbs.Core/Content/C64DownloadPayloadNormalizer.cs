using System.IO.Compression;

namespace Bbs.Core.Content;

public static class C64DownloadPayloadNormalizer
{
    public const long DefaultMaxArchiveBytes = 100L * 1024 * 1024;
    private const string MaxArchiveBytesEnvironmentVariable = "C64_ARCHIVE_MAX_BYTES";
    private static readonly string[] PreferredExtensions =
    {
        ".prg", ".p00", ".t64", ".d64", ".d71", ".d81", ".d82", ".zip", ".gz"
    };

    public static DownloadPayload Normalize(string fileName, byte[] content, string sourceUrl, long? maxArchiveBytes = null)
    {
        var safeFileName = string.IsNullOrWhiteSpace(fileName) ? "download.bin" : fileName;

        if (content is null || content.Length == 0)
        {
            return new DownloadPayload(safeFileName, content ?? Array.Empty<byte>(), sourceUrl);
        }

        var ext = Path.GetExtension(safeFileName).ToLowerInvariant();
        var limit = maxArchiveBytes ?? ResolveMaxArchiveBytes();
        if (ext == ".zip")
        {
            EnsureWithinLimit(content.LongLength, limit, "Compressed ZIP archive");
            var unzipped = TryExtractFromZip(content, sourceUrl, limit);
            if (unzipped is not null)
            {
                return unzipped;
            }
        }
        else if (ext == ".gz")
        {
            EnsureWithinLimit(content.LongLength, limit, "Compressed GZip archive");
            var gunzipped = TryExtractFromGzip(safeFileName, content, sourceUrl, limit);
            if (gunzipped is not null)
            {
                return gunzipped;
            }
        }

        return new DownloadPayload(safeFileName, content, sourceUrl);
    }

    private static DownloadPayload? TryExtractFromZip(byte[] content, string sourceUrl, long limit)
    {
        using var ms = new MemoryStream(content);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: false);

        if (zip.Entries.Count == 0)
        {
            return null;
        }

        var candidates = zip.Entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Name))
            .ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }

        ZipArchiveEntry selected = candidates[0];
        foreach (var preferred in PreferredExtensions)
        {
            var hit = candidates.FirstOrDefault(e => e.Name.EndsWith(preferred, StringComparison.OrdinalIgnoreCase));
            if (hit is not null)
            {
                selected = hit;
                break;
            }
        }

        EnsureWithinLimit(selected.Length, limit, "ZIP entry");
        using var entryStream = selected.Open();
        return new DownloadPayload(selected.Name, ReadBounded(entryStream, limit, "ZIP entry"), sourceUrl);
    }

    private static DownloadPayload? TryExtractFromGzip(string fileName, byte[] content, string sourceUrl, long limit)
    {
        using var ms = new MemoryStream(content);
        using var gz = new GZipStream(ms, CompressionMode.Decompress, leaveOpen: false);

        var outputName = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(outputName))
        {
            outputName = "download.bin";
        }

        var bytes = ReadBounded(gz, limit, "GZip output");
        if (bytes.Length == 0)
        {
            return null;
        }

        return new DownloadPayload(outputName, bytes, sourceUrl);
    }

    private static long ResolveMaxArchiveBytes()
    {
        var configured = Environment.GetEnvironmentVariable(MaxArchiveBytesEnvironmentVariable);
        return long.TryParse(configured, out var value) && value > 0 ? value : DefaultMaxArchiveBytes;
    }

    private static byte[] ReadBounded(Stream input, long limit, string subject)
    {
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = input.Read(buffer, 0, buffer.Length);
            if (read == 0) return output.ToArray();
            EnsureWithinLimit(output.Length + read, limit, subject);
            output.Write(buffer, 0, read);
        }
    }

    private static void EnsureWithinLimit(long size, long limit, string subject)
    {
        if (size > limit)
            throw new InvalidDataException($"{subject} exceeds the configured {limit} byte limit.");
    }
}
