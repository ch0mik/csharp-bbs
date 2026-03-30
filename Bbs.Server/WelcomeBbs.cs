using Bbs.Terminals;

namespace Bbs.Server;

public sealed class WelcomeBbs : PetsciiThread
{
    public override async Task DoLoopAsync(CancellationToken cancellationToken = default)
    {
        Cls();
        Println("This is your brand-new BBS");
        Println();
        Print("Enter your name: ");
        await FlushAsync(cancellationToken).ConfigureAwait(false);
        await ResetInputAsync(cancellationToken).ConfigureAwait(false);

        var name = await ReadLineAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "guest";
        }

        Println();
        Println($"Welcome, {name}!");
        Println("Type text and press ENTER. Type /quit to exit.");

        while (!cancellationToken.IsCancellationRequested)
        {
            Print("> ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);

            var line = await ReadLineAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (string.Equals(line, "/quit", StringComparison.OrdinalIgnoreCase))
            {
                Println("Bye!");
                await FlushAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            if (!string.IsNullOrWhiteSpace(line))
            {
                Println($"Echo: {line}");
            }
        }
    }
}
