using Bbs.Terminals;

namespace Bbs.Tenants;

public sealed class WarGamesPetscii : PetsciiThread
{
    public override async Task DoLoopAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Cls();
            Println("IMSAI 8080 COMMUNICATIONS");
            Println("WARGAMES SIMULATOR V2");
            Println("---------------------------------------");
            Println("1) CALL SCHOOL DISTRICT");
            Println("2) CINEMATIC PATH / WAR DIAL");
            Println("3) STRATEGIC GTW SIMULATION");
            Println(".) HANG UP AND RETURN TO BBS");
            Println();
            Print("COMMAND: ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);
            var input = (await ReadLineAsync(20, cancellationToken).ConfigureAwait(false)).Trim().ToUpperInvariant();
            if (input is "." or "Q" or "QUIT" or "EXIT") return;

            if (input is "1" or "S" or "SCHOOL" or "PENCIL")
            {
                await ShowDialSequenceAsync("3115550127", cancellationToken).ConfigureAwait(false);
                await LaunchAsync(new SchoolPetscii(), cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (input is "2" or "D" or "DIALER" or "WAR DIALER" or "WARDIALER")
            {
                await LaunchAsync(new WarDialerPetscii(), cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (input is "3" or "S" or "STRATEGIC" or "SIMULATION")
            {
                await LaunchAsync(new ThermonuclearWarPetscii(), cancellationToken).ConfigureAwait(false);
                continue;
            }

            Println("UNKNOWN COMMAND");
            Print("PRESS ENTER: ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);
            await ReadLineAsync(1, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ShowDialSequenceAsync(string number, CancellationToken token)
    {
        Cls();
        Println($"ATD{number}");
        Println("DIALING...");
        await FlushAsync(token).ConfigureAwait(false);
        await Task.Delay(TimeSpan.FromMilliseconds(350), token).ConfigureAwait(false);
        Println("CONNECT 1200");
        await FlushAsync(token).ConfigureAwait(false);
        await Task.Delay(TimeSpan.FromMilliseconds(350), token).ConfigureAwait(false);
    }
}
