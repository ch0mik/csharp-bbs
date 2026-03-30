using System.IO.Compression;

namespace Bbs.Core.Content;

public static class C64DownloadPayloadNormalizer
{
    private static readonly string[] PreferredExtensions =
    {
        ".prg", ".p00", ".t64", ".d64", ".d71", ".d81", ".d82", ".zip", ".gz"
    };

    public static DownloadPayload Normalize(string fileName, byte[] content, string sourceUrl)
    {
        var safeFileName = string.IsNullOrWhiteSpace(fileName) ? "download.bin" : fileName;

        if (content is null || content.Length == 0)
        {
            return new DownloadPayload(safeFileName, content ?? Array.Empty<byte>(), sourceUrl);
        }

        var ext = Path.GetExtension(safeFileName).ToLowerInvariant();
        if (ext == ".zip")
        {
            var unzipped = TryExtractFromZip(content, sourceUrl);
            if (unzipped is not null)
            {
                return unzipped;
            }
        }
        else if (ext == ".gz")
        {
            var gunzipped = TryExtractFromGzip(safeFileName, content, sourceUrl);
            if (gunzipped is not null)
            {
                return gunzipped;
            }
        }

        return new DownloadPayload(safeFileName, content, sourceUrl);
    }

    private static DownloadPayload? TryExtractFromZip(byte[] content, string sourceUrl)
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

        using var entryStream = selected.Open();
        using var outMs = new MemoryStream();
        entryStream.CopyTo(outMs);
        return new DownloadPayload(selected.Name, outMs.ToArray(), sourceUrl);
    }

    private static DownloadPayload? TryExtractFromGzip(string fileName, byte[] content, string sourceUrl)
    {
        using var ms = new MemoryStream(content);
        using var gz = new GZipStream(ms, CompressionMode.Decompress, leaveOpen: false);
        using var outMs = new MemoryStream();
        gz.CopyTo(outMs);

        var outputName = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(outputName))
        {
            outputName = "download.bin";
        }

        var bytes = outMs.ToArray();
        if (bytes.Length == 0)
        {
            return null;
        }

        return new DownloadPayload(outputName, bytes, sourceUrl);
    }
}
