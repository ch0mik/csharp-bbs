using Bbs.Core.Net;
using Bbs.Petsciiator;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Bbs.Tenants.Content;

public sealed class WordpressImageRenderer : IDisposable
{
    private static readonly TimeSpan RedisTtl = TimeSpan.FromDays(7);
    private static readonly PetsciiatorOptions OnlineWordpressOptions = new()
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
                rendered = await _converter.ConvertAsync(bytes, OnlineWordpressOptions, cancellationToken).ConfigureAwait(false);
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
        return $"bbs:wordpress:petscii:v1:{hex}";
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
        Console.WriteLine($"[{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}][WordpressImageRenderer] {message}");
    }
}
