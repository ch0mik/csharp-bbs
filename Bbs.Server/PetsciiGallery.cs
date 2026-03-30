using Bbs.Terminals;

namespace Bbs.Server;

public sealed class PetsciiGallery : PetsciiThread
{
    private static readonly string[] Pages =
    {
        "[PETSCII GALLERY]\n\nPAGE 1/3\n\n  ***   *   *\n *   *  **  *\n *****  * * *\n *   *  *  **\n *   *  *   *\n\nRetro wave #1",
        "[PETSCII GALLERY]\n\nPAGE 2/3\n\n ####   ###\n #   # #   #\n ####  #   #\n #     #   #\n #      ###\n\nRetro wave #2",
        "[PETSCII GALLERY]\n\nPAGE 3/3\n\n +----------------------+\n |   PETSCII FOREVER    |\n |  C64 BBS EXPERIENCE  |\n +----------------------+\n\nRetro wave #3"
    };

    public override async Task DoLoopAsync(CancellationToken cancellationToken = default)
    {
        var page = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            Cls();
            Println(Pages[page]);
            Println();
            Println("N) Next  P) Prev  Q) Back");
            Print("Choice: ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);

            var key = (await ReadLineAsync(maxLength: 8, cancellationToken: cancellationToken).ConfigureAwait(false))
                .Trim()
                .ToUpperInvariant();

            if (key is "Q" or "QUIT" or "BACK" or "X")
            {
                return;
            }

            if (key is "N" or "NEXT" or "")
            {
                page = (page + 1) % Pages.Length;
                continue;
            }

            if (key is "P" or "PREV" or "PREVIOUS")
            {
                page = (page - 1 + Pages.Length) % Pages.Length;
            }
        }
    }
}
