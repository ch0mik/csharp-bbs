using Bbs.Tenants.Content;

namespace Bbs.Tenants;

public sealed class CommodoreNews : WordpressProxyPetscii
{
    protected override string Domain => "https://commodore.net";

    protected override string SourceLabel => "Commodore News";
}
