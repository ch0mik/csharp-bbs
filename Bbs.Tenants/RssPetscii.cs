using Bbs.Tenants.Content;

namespace Bbs.Tenants;

public sealed class RssPetscii : RssPetsciiBase
{
    protected override string Title => "RSS PETSCII";

    protected override IReadOnlyDictionary<string, (string Label, string Url)> Channels => new Dictionary<string, (string Label, string Url)>(StringComparer.OrdinalIgnoreCase)
    {
        ["1"] = ("BBC", "https://feeds.bbci.co.uk/news/world/rss.xml"),
        ["2"] = ("Hacker News", "https://hnrss.org/frontpage"),
        ["3"] = ("CSDb Latest Releases", "https://csdb.dk/rss/latestreleases.php")
    };
}
