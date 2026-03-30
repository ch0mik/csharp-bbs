using Bbs.Tenants.Content;

namespace Bbs.Tenants;

public sealed class WordpressProxy : WordpressProxyPetscii
{
    protected override string Domain => "https://wordpress.org/news";
}
