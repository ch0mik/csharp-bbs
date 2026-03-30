using Bbs.Core;
using Bbs.Terminals;

namespace Bbs.Tenants;

[Hidden]
public sealed class StdChoice : PetsciiThread
{
    public override async Task DoLoopAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Cls();
            Println("PETSCII BBS - StdChoice");
            Println();
            Println("1) WelcomeBbs");
            Println("Q) Quit");
            Println();
            Print("Choice: ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);

            var choice = (await ReadLineAsync(maxLength: 8, cancellationToken: cancellationToken).ConfigureAwait(false))
                .Trim()
                .ToUpperInvariant();

            if (choice is "Q" or "QUIT" or "X")
            {
                Println();
                Println("Bye!");
                await FlushAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            if (choice is "1" or "WELCOME" or "WELCOMEBBS")
            {
                await LaunchAsync(new WelcomeBbs(), cancellationToken).ConfigureAwait(false);
                continue;
            }

            Println();
            Println("Unknown option.");
            Println("Press ENTER...");
            await FlushAsync(cancellationToken).ConfigureAwait(false);
            await ReadLineAsync(maxLength: 1, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}

