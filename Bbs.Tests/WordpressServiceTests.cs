using Bbs.Core.Content;
using Bbs.Core.Net;

namespace Bbs.Tests;

public class WordpressServiceTests
{
    [Fact]
    public async Task GetPostsAsync_ShouldBuildApiUrlFromDomainRoot_WhenInputContainsPath()
    {
        var http = new RecordingHttpService("""[{"id":1,"title":{"rendered":"Hello"},"excerpt":{"rendered":"World"},"date":"2026-06-27T10:00:00Z","author":2}]""");
        var service = new WordpressService(http);

        var posts = await service.GetPostsAsync("https://commodore.net/news/", 1, 10).ConfigureAwait(false);

        Assert.Single(posts);
        Assert.Equal("https://commodore.net/wp-json/wp/v2/posts?context=view&page=1&per_page=10", http.LastUrl);
    }

    [Fact]
    public async Task GetPostAsync_ShouldBuildApiUrlFromDomainRoot_WhenInputContainsPath()
    {
        var http = new RecordingHttpService("""{"id":2674,"title":{"rendered":"Commodore"},"content":{"rendered":"News"},"excerpt":{"rendered":"Excerpt"},"date":"2026-06-27T10:00:00Z","author":2,"featured_media":5}""");
        var service = new WordpressService(http);

        var post = await service.GetPostAsync("https://commodore.net/news/", 2674).ConfigureAwait(false);

        Assert.Equal(2674, post.Id);
        Assert.Equal("https://commodore.net/wp-json/wp/v2/posts/2674?context=view", http.LastUrl);
    }

    private sealed class RecordingHttpService(string response) : IHttpService
    {
        public string? LastUrl { get; private set; }

        public Task<string> GetStringAsync(string url, string? userAgent = null, CancellationToken cancellationToken = default)
        {
            LastUrl = url;
            return Task.FromResult(response);
        }

        public Task<byte[]> GetBytesAsync(string url, string? userAgent = null, CancellationToken cancellationToken = default)
        {
            LastUrl = url;
            return Task.FromResult(Array.Empty<byte>());
        }
    }
}
