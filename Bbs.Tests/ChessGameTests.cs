using Bbs.Tenants.Content.Chess;

namespace Bbs.Tests;

public class ChessGameTests
{
    [Fact]
    public void OpeningPawn_CanMoveOneOrTwoSquares()
    {
        var one = new ChessBoard();
        var two = new ChessBoard();

        Assert.True(one.TryMove("E2E3", true));
        Assert.True(two.TryMove("e2-e4", true));
        Assert.Equal('P', two[4, 3]);
    }

    [Fact]
    public void Pieces_CannotMoveThroughOtherPieces()
    {
        var board = new ChessBoard();

        Assert.False(board.TryMove("A1A4", true));
        Assert.False(board.TryMove("C1H6", true));
    }

    [Fact]
    public void KnightAndDeterministicComputer_HaveLegalMoves()
    {
        var board = new ChessBoard();

        Assert.True(board.TryMove("G1F3", true));
        var reply = board.BestMove(false);
        Assert.NotNull(reply);
        Assert.True(board.TryMove(reply, false));
    }
}
