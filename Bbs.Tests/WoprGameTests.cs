using Bbs.Tenants.Content.Wopr;

namespace Bbs.Tests;

public class WoprGameTests
{
    [Theory]
    [InlineData("1", "UNITED STATES")]
    [InlineData("usa", "UNITED STATES")]
    [InlineData("  soviet   union ", "SOVIET UNION")]
    [InlineData("Russia", "SOVIET UNION")]
    public void ParseSide_AcceptsUsefulAliases(string input, string expected)
        => Assert.Equal(expected, WoprInput.ParseSide(input));

    [Fact]
    public void BestMove_CompletesWinningRow()
    {
        var board = new TicTacToeBoard();
        Assert.True(board.TryMove(0, 'X'));
        Assert.True(board.TryMove(1, 'X'));
        Assert.Equal(2, board.BestMove('X'));
    }

    [Fact]
    public void BestMove_BlocksOpponent()
    {
        var board = new TicTacToeBoard();
        Assert.True(board.TryMove(0, 'O'));
        Assert.True(board.TryMove(1, 'O'));
        Assert.Equal(2, board.BestMove('X'));
    }

    [Fact]
    public void PerfectComputers_EndInDraw()
    {
        var board = new TicTacToeBoard();
        var mark = 'X';
        while (!board.IsFull && board.Winner() == '\0')
        {
            Assert.True(board.TryMove(board.BestMove(mark), mark));
            mark = mark == 'X' ? 'O' : 'X';
        }

        Assert.Equal('\0', board.Winner());
        Assert.True(board.IsFull);
    }
}
