using System.Text.Json.Serialization;

namespace Bbs.Tenants.Content.Quiz;

internal sealed class QuizPackHeader
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("createdAt")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }
}

internal sealed class QuizQuestion
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("q")]
    public string Q { get; set; } = string.Empty;

    [JsonPropertyName("a")]
    public string A { get; set; } = string.Empty;

    [JsonPropertyName("b")]
    public string B { get; set; } = string.Empty;

    [JsonPropertyName("c")]
    public string C { get; set; } = string.Empty;

    [JsonPropertyName("d")]
    public string D { get; set; } = string.Empty;

    [JsonPropertyName("correct")]
    public string Correct { get; set; } = string.Empty;
}

internal sealed class QuizPackDocument
{
    [JsonPropertyName("header")]
    public QuizPackHeader? Header { get; set; }

    [JsonPropertyName("questions")]
    public List<QuizQuestion>? Questions { get; set; }
}

internal sealed record QuizPack(string SourcePath, QuizPackHeader Header, IReadOnlyList<QuizQuestion> Questions);
