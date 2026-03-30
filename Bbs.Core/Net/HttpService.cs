using System.Net;
using System.Text;

namespace Bbs.Core.Net;

public interface IHttpService
{
    Task<string> GetStringAsync(string url, string? userAgent = null, CancellationToken cancellationToken = default);

    Task<byte[]> GetBytesAsync(string url, string? userAgent = null, CancellationToken cancellationToken = default);
}

public sealed class HttpService : IHttpService
{
    private readonly HttpClient _client;

    public HttpService(HttpClient? client = null)
    {
        _client = client ?? BuildDefaultClient();
    }

    public async Task<string> GetStringAsync(string url, string? userAgent = null, CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(url, userAgent);
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<byte[]> GetBytesAsync(string url, string? userAgent = null, CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(url, userAgent);
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    private static HttpRequestMessage BuildRequest(string url, string? userAgent)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", string.IsNullOrWhiteSpace(userAgent) ? "PETSCII BBS by 8-bitz" : userAgent.Trim());
        request.Headers.TryAddWithoutValidation("Accept", "*/*");
        return request;
    }

    private static HttpClient BuildDefaultClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 8
        };

        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(45),
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };
    }
}

