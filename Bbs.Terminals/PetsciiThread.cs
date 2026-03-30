using Bbs.Core;
using System.Net.Sockets;

namespace Bbs.Terminals;

public abstract class PetsciiThread : BbsThread
{
    public override BbsInputOutput BuildIO(TcpClient client) => new PetsciiInputOutput(client);

    public override int GetScreenColumns() => 40;

    public override int GetScreenRows() => 25;

    public override void Cls() => Write(PetsciiKeys.Cls);

    public override async Task InitBbsAsync(CancellationToken cancellationToken = default)
    {
        // Initialize PETSCII lowercase/uppercase charset (readable text) on session start.
        System.Console.WriteLine($"[DEBUG PetsciiThread.InitBbsAsync] Sending LOWERCASE init: {PetsciiKeys.CaseUnlock},{PetsciiKeys.Lowercase},{PetsciiKeys.CaseLock}");
        Write(PetsciiKeys.CaseUnlock, PetsciiKeys.Lowercase, PetsciiKeys.CaseLock);
        await FlushAsync(cancellationToken).ConfigureAwait(false);
        System.Console.WriteLine("[DEBUG PetsciiThread.InitBbsAsync] LOWERCASE codes sent");
    }

    // Note: LOWERCASE mode initialization is done in root AutoDetectTerminal, not here,
    // to avoid sending codes multiple times when launching sub-menus.

    public async Task SetLowercaseModeAsync(CancellationToken cancellationToken = default)
    {
        Write(PetsciiKeys.CaseUnlock, PetsciiKeys.Lowercase, PetsciiKeys.CaseLock);
        await FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetUppercaseModeAsync(CancellationToken cancellationToken = default)
    {
        Write(PetsciiKeys.CaseUnlock, PetsciiKeys.Uppercase, PetsciiKeys.CaseLock);
        await FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
