namespace Bbs.Core.Content;

public sealed record RssEntry(
    string Title,
    string Description,
    string Link,
    DateTimeOffset? PublishedAt);

public sealed record WordpressPostSummary(
    long Id,
    string Title,
    string Excerpt,
    DateTimeOffset? PublishedAt,
    long? AuthorId);

public sealed record WordpressPostDetails(
    long Id,
    string Title,
    string Content,
    string Excerpt,
    DateTimeOffset? PublishedAt,
    long? AuthorId);

public sealed record WikipediaSearchItem(
    string Language,
    long PageId,
    string Title,
    string Snippet,
    long WordCount,
    long Size,
    string Timestamp);

public sealed record CsdbReleaseItem(
    string Id,
    string Title,
    string Type,
    string ReleaseUri,
    string ReleasedBy,
    DateTimeOffset? PublishedAt,
    string? DownloadLink);

public sealed record DownloadPayload(string FileName, byte[] Content, string SourceUrl);

public interface IRssService
{
    Task<IReadOnlyList<RssEntry>> ReadFeedAsync(string url, CancellationToken cancellationToken = default);
}

public interface IWordpressService
{
    Task<IReadOnlyList<WordpressPostSummary>> GetPostsAsync(string domain, int page, int pageSize, string? categoriesId = null, string? userAgent = null, CancellationToken cancellationToken = default);

    Task<WordpressPostDetails> GetPostAsync(string domain, long postId, string? userAgent = null, CancellationToken cancellationToken = default);

    Task<string?> GetAuthorNameAsync(string domain, long authorId, string? userAgent = null, CancellationToken cancellationToken = default);
}

public interface IWikipediaService
{
    Task<IReadOnlyList<WikipediaSearchItem>> SearchAsync(string language, string query, int limit = 20, CancellationToken cancellationToken = default);

    Task<WikipediaSearchItem?> PickRandomAsync(string language, CancellationToken cancellationToken = default);

    Task<string> GetArticleHtmlAsync(string language, long pageId, CancellationToken cancellationToken = default);
}

public interface ICsdbService
{
    Task<IReadOnlyList<CsdbReleaseItem>> GetLatestReleasesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CsdbReleaseItem>> SearchReleasesAsync(string query, CancellationToken cancellationToken = default);

    Task<DownloadPayload?> DownloadReleaseAsync(CsdbReleaseItem item, CancellationToken cancellationToken = default);
}

public interface IZMachineService
{
    Task RunAsync(string gameName, byte[] storyData, Func<string, Task> onOutput, Func<Task<string>> onInput, CancellationToken cancellationToken = default);
}

public interface IPetsciiGalleryService
{
    Task<IReadOnlyList<string>> ListAuthorsAsync(string rootPath, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListDrawingsAsync(string authorPath, CancellationToken cancellationToken = default);

    Task<byte[]> ReadDrawingAsync(string drawingPath, CancellationToken cancellationToken = default);
}
