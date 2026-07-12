using Bbs.Terminals;
using Bbs.Tenants.Content.Chess;

namespace Bbs.Tenants;

public sealed class ChessPetscii : PetsciiThread
{
    public override async Task DoLoopAsync(CancellationToken cancellationToken = default)
    {
        var board = new ChessBoard();
        while (!cancellationToken.IsCancellationRequested)
        {
            Cls();
            Println("A NICE GAME OF CHESS");
            Println("YOU ARE WHITE (UPPERCASE)");
            Draw(board);
            Println("MOVE: E2E4   .) RETURN TO WOPR");
            Print("WHITE> ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);
            var input = (await ReadLineAsync(8, cancellationToken).ConfigureAwait(false)).Trim();
            if (input.ToUpperInvariant() is "." or "QUIT" or "EXIT") return;
            if (!board.TryMove(input, true))
            {
                Println("ILLEGAL MOVE - PRESS ENTER");
                await FlushAsync(cancellationToken).ConfigureAwait(false);
                await ReadLineAsync(1, cancellationToken).ConfigureAwait(false);
                continue;
            }
            if (!board.HasKing(false)) { await FinishAsync("WHITE WINS", cancellationToken).ConfigureAwait(false); return; }

            var reply = board.BestMove(false);
            if (reply is null) { await FinishAsync("DRAW", cancellationToken).ConfigureAwait(false); return; }
            board.TryMove(reply, false);
            Println($"WOPR> {reply}");
            if (!board.HasKing(true)) { await FinishAsync("WOPR WINS", cancellationToken).ConfigureAwait(false); return; }
        }
    }

    private void Draw(ChessBoard board)
    {
        Println("    A B C D E F G H");
        for (var rank = 7; rank >= 0; rank--)
        {
            Print($" {rank + 1}  ");
            for (var file = 0; file < 8; file++) Print($"{board[file, rank]} ");
            Println($" {rank + 1}");
        }
        Println("    A B C D E F G H");
    }

    private async Task FinishAsync(string result, CancellationToken token)
    {
        Println(result);
        Print("PRESS ENTER: ");
        await FlushAsync(token).ConfigureAwait(false);
        await ReadLineAsync(1, token).ConfigureAwait(false);
    }
}
