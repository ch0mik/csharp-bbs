using System.Text.Json;

namespace Bbs.Tenants.Content.Quiz;

internal sealed class QuizPackLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly string _packsRoot;

    public QuizPackLoader(string? packsRoot = null)
    {
        _packsRoot = ResolvePacksRoot(packsRoot);
    }

    public string PacksRoot => _packsRoot;

    public IReadOnlyList<QuizPack> LoadAll()
    {
        if (string.IsNullOrWhiteSpace(_packsRoot) || !Directory.Exists(_packsRoot))
        {
            return Array.Empty<QuizPack>();
        }

        var files = Directory.GetFiles(_packsRoot, "*.json", SearchOption.AllDirectories)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var result = new List<QuizPack>();
        foreach (var file in files)
        {
            try
            {
                var pack = LoadPack(file);
                result.Add(pack);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}][QuizPackLoader] skipped '{file}': {ex.Message}");
            }
        }

        return result
            .OrderBy(x => x.Header.Language, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Header.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ResolvePacksRoot(string? preferred)
    {
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            return preferred.Trim();
        }

        var env = Environment.GetEnvironmentVariable("QUIZ_PACKS_ROOT")?.Trim();
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env;
        }

        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "quiz-packs"),
            Path.Combine(Directory.GetCurrentDirectory(), "quiz-packs")
        };

        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && cursor is not null; i++)
        {
            candidates.Add(Path.Combine(cursor.FullName, "quiz-packs"));
            cursor = cursor.Parent;
        }

        var found = candidates.FirstOrDefault(Directory.Exists);
        return found ?? Path.Combine(AppContext.BaseDirectory, "quiz-packs");
    }

    private static QuizPack LoadPack(string path)
    {
        var json = File.ReadAllText(path);
        var doc = JsonSerializer.Deserialize<QuizPackDocument>(json, JsonOptions)
            ?? throw new InvalidOperationException("Invalid JSON content.");

        var header = doc.Header ?? throw new InvalidOperationException("Missing required 'header' object.");
        header.Id = Require(header.Id, "header.id");
        header.Language = Require(header.Language, "header.language").ToLowerInvariant();
        if (header.Language is not "pl" and not "en" and not "cz")
        {
            throw new InvalidOperationException("header.language must be 'pl', 'en' or 'cz'.");
        }

        header.Title = Require(header.Title, "header.title");
        header.Description = Require(header.Description, "header.description");
        header.Version = Require(header.Version, "header.version");

        var questions = doc.Questions ?? throw new InvalidOperationException("Missing required 'questions' array.");
        if (questions.Count < 25)
        {
            throw new InvalidOperationException("Quiz pack must contain at least 25 questions.");
        }

        var normalized = new List<QuizQuestion>(questions.Count);
        for (var i = 0; i < questions.Count; i++)
        {
            var q = questions[i] ?? throw new InvalidOperationException($"Question at index {i} is null.");
            q.Id = Require(q.Id, $"questions[{i}].id");
            q.Q = Require(q.Q, $"questions[{i}].q");
            q.A = Require(q.A, $"questions[{i}].a");
            q.B = Require(q.B, $"questions[{i}].b");
            q.C = Require(q.C, $"questions[{i}].c");
            q.D = Require(q.D, $"questions[{i}].d");

            var correct = Require(q.Correct, $"questions[{i}].correct").ToUpperInvariant();
            if (correct is not "A" and not "B" and not "C" and not "D")
            {
                throw new InvalidOperationException($"questions[{i}].correct must be one of A/B/C/D.");
            }

            q.Correct = correct;
            normalized.Add(q);
        }

        return new QuizPack(path, header, normalized);
    }

    private static string Require(string? value, string fieldName)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException($"Missing required field '{fieldName}'.");
        }

        return trimmed;
    }
}
