using System.Text.RegularExpressions;

namespace Bbs.Tenants.Content;

internal static class TextRender
{
    public static string SanitizeHtmlToText(string html)
    {
        var noScript = Regex.Replace(html ?? string.Empty, "<script.*?</script>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var noStyle = Regex.Replace(noScript, "<style.*?</style>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var noTags = Regex.Replace(noStyle, "<.*?>", " ", RegexOptions.Singleline);
        var decoded = System.Net.WebUtility.HtmlDecode(noTags);
        return Regex.Replace(decoded, "\\s+", " ").Trim();
    }

    public static IReadOnlyList<string> WrapLines(string text, int width)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = string.Empty;

        foreach (var word in words)
        {
            if ((current.Length == 0 ? 0 : current.Length + 1) + word.Length <= width)
            {
                current = current.Length == 0 ? word : current + " " + word;
            }
            else
            {
                lines.Add(current);
                current = word;
            }
        }

        if (current.Length > 0)
        {
            lines.Add(current);
        }

        return lines;
    }

    public static string TrimTo(string value, int max)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= max ? value : value[..Math.Max(0, max - 1)] + "...";
    }
}
