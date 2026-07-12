using Bbs.Core.Content;
using Bbs.Core.Net;

namespace Bbs.Tests;

public class WikipediaServiceTests
{
    [Fact]
    public async Task Search_UsesWikimediaCompliantUserAgent()
    {
        var http = new RecordingHttpService("""{"query":{"search":[]}}""");
        var service = new WikipediaService(http);

        await service.SearchAsync("en", "commodore");

        Assert.Equal(WikipediaService.WikimediaUserAgent, http.LastUserAgent);
        Assert.Contains("github.com/ch0mik/csharp-bbs", http.LastUserAgent, StringComparison.Ordinal);
        Assert.Contains("Bot", http.LastUserAgent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AllWikipediaApiCalls_UseTheSameUserAgent()
    {
        var http = new QueueHttpService(
            """{"query":{"random":[]}}""",
            """{"parse":{"text":"<p>test</p>"}}""",
            """{"query":{"pages":[]}}""");
        var service = new WikipediaService(http);

        await service.PickRandomAsync("pl");
        await service.GetArticleHtmlAsync("pl", 1);
        await service.GetArticleImageUrlsAsync("pl", 1);

        Assert.Equal(3, http.UserAgents.Count);
        Assert.All(http.UserAgents, value => Assert.Equal(WikipediaService.WikimediaUserAgent, value));
    }

    private sealed class RecordingHttpService(string response) : IHttpService
    {
        public string? LastUserAgent { get; private set; }
        public Task<string> GetStringAsync(string url, string? userAgent = null, CancellationToken cancellationToken = default)
        {
            LastUserAgent = userAgent;
            return Task.FromResult(response);
        }
        public Task<byte[]> GetBytesAsync(string url, string? userAgent = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<byte>());
    }

    private sealed class QueueHttpService(params string[] responses) : IHttpService
    {
        private readonly Queue<string> _responses = new(responses);
        public List<string?> UserAgents { get; } = new();
        public Task<string> GetStringAsync(string url, string? userAgent = null, CancellationToken cancellationToken = default)
        {
            UserAgents.Add(userAgent);
            return Task.FromResult(_responses.Dequeue());
        }
        public Task<byte[]> GetBytesAsync(string url, string? userAgent = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<byte>());
    }
}
