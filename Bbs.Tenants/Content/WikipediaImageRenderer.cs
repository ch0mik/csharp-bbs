using Bbs.Core.Net;
using Bbs.Petsciiator;
using SixLabors.ImageSharp;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Bbs.Tenants.Content;

public sealed class WikipediaImageRenderer : IDisposable
{
    private const int MinWidth = 320;
    private const int MinHeight = 200;

    private static readonly TimeSpan RedisTtl = TimeSpan.FromDays(7);
    private static readonly PetsciiatorOptions OnlineWikiOptions = new()
    {
        BbsCompatibleOutput = true,
        BbsColumns = 39,
        PreColorCount = 32,
        PreDither = true
    };

    private readonly PetsciiatorConverter _converter = new();
    private readonly IHttpService _http = new HttpService();
    private readonly ConcurrentDictionary<string, byte[]> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);
    private readonly RedisPetsciiCache? _redis = RedisPetsciiCache.CreateFromEnvironment();

    public async Task<byte[]> RenderAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            DebugLog("RenderAsync skipped: empty image URL.");
            return Array.Empty<byte>();
        }

        var key = BuildRedisKey(url);
        DebugLog($"Render start: url='{url}', redis_enabled={_redis is not null}, key='{key}'");
        if (_redis is not null && _redis.TryGet(key, out var redisBytes))
        {
            DebugLog($"Redis cache HIT: key='{key}', bytes={redisBytes.Length}");
            return redisBytes;
        }
        if (_redis is not null)
        {
            DebugLog($"Redis cache MISS: key='{key}'");
        }

        if (_redis is null && _cache.TryGetValue(url, out var cached))
        {
            DebugLog($"In-memory cache HIT: url='{url}', bytes={cached.Length}");
            return cached;
        }

        var gate = _locks.GetOrAdd(url, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_redis is not null && _redis.TryGet(key, out redisBytes))
            {
                DebugLog($"Redis cache HIT (post-lock): key='{key}', bytes={redisBytes.Length}");
                return redisBytes;
            }
            if (_redis is not null)
            {
                DebugLog($"Redis cache MISS (post-lock): key='{key}'");
            }

            if (_redis is null && _cache.TryGetValue(url, out cached))
            {
                DebugLog($"In-memory cache HIT (post-lock): url='{url}', bytes={cached.Length}");
                return cached;
            }

            byte[] rendered;
            try
            {
                var bytes = await _http.GetBytesAsync(url, cancellationToken: cancellationToken).ConfigureAwait(false);
                DebugLog($"Image download OK: url='{url}', source_bytes={bytes.Length}");

                if (!TryGetImageSize(bytes, out var width, out var height))
                {
                    DebugLog($"Image skipped: unable to identify size: url='{url}'");
                    return Array.Empty<byte>();
                }

                if (width < MinWidth || height < MinHeight)
                {
                    DebugLog($"Image skipped: too small for wiki inline render: url='{url}', size={width}x{height}, min={MinWidth}x{MinHeight}");
                    return Array.Empty<byte>();
                }

                rendered = await _converter.ConvertAsync(bytes, OnlineWikiOptions, cancellationToken).ConfigureAwait(false);
                DebugLog($"Image convert OK: url='{url}', petscii_bytes={rendered.Length}");
            }
            catch (Exception ex)
            {
                DebugLog($"Image render FAILED: url='{url}', error='{ex.Message}'");
                rendered = Array.Empty<byte>();
            }

            if (rendered.Length > 0)
            {
                if (_redis is not null)
                {
                    _redis.Set(key, rendered, RedisTtl);
                    DebugLog($"Redis cache SET: key='{key}', bytes={rendered.Length}, ttl_days={RedisTtl.TotalDays}");
                }
                else
                {
                    _cache[url] = rendered;
                    DebugLog($"In-memory cache SET: url='{url}', bytes={rendered.Length}");
                }
            }
            else
            {
                DebugLog($"Image render produced empty output: url='{url}'");
            }

            return rendered;
        }
        finally
        {
            gate.Release();
        }
    }

    private static string BuildRedisKey(string url)
    {
        var normalized = url.Trim();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        return $"bbs:wikipedia:petscii:v2:{hex}";
    }

    public void Dispose()
    {
        foreach (var gate in _locks.Values)
        {
            gate.Dispose();
        }

        _converter.Dispose();
    }

    private static void DebugLog(string message)
    {
        Console.WriteLine($"[{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}][WikipediaImageRenderer] {message}");
    }

    private static bool TryGetImageSize(byte[] bytes, out int width, out int height)
    {
        width = 0;
        height = 0;
        try
        {
            var info = Image.Identify(bytes);
            if (info is null)
            {
                return false;
            }

            width = info.Width;
            height = info.Height;
            return width > 0 && height > 0;
        }
        catch
        {
            return false;
        }
    }
}
