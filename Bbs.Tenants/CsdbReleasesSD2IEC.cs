using Bbs.Terminals;

namespace Bbs.Tenants;

public sealed class CsdbReleasesSD2IEC : PetsciiThread
{
    private readonly CsdbReleases _base = new(enableXmodemDownloads: true);

    public override async Task DoLoopAsync(CancellationToken cancellationToken = default)
    {
        Cls();
        Println("CsdbReleases SD2IEC mode");
        Println("D# starts XMODEM download");
        Println();
        Print("Press ENTER to continue...");
        await FlushAsync(cancellationToken).ConfigureAwait(false);
        await ReadLineAsync(maxLength: 1, cancellationToken: cancellationToken).ConfigureAwait(false);

        await LaunchAsync(_base, cancellationToken).ConfigureAwait(false);
    }
}
