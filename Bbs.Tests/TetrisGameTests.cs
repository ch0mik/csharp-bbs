using Bbs.Tenants.Content.Games;

namespace Bbs.Tests;

public class TetrisGameTests
{
    [Fact]
    public void NewGame_HasTenByTwentyBoardAndActivePiece()
    {
        var game = new TetrisGame(seed: 1);
        var occupied = 0;
        for (var y = 0; y < TetrisGame.Height; y++)
            for (var x = 0; x < TetrisGame.Width; x++)
                if (game.CellAt(x, y) != 0) occupied++;

        Assert.Equal(10, TetrisGame.Width);
        Assert.Equal(20, TetrisGame.Height);
        Assert.Equal(4, occupied);
        Assert.False(game.IsGameOver);
    }

    [Fact]
    public void Piece_CanMoveAndDropWithoutLeavingBoard()
    {
        var game = new TetrisGame(seed: 2);
        for (var i = 0; i < 20; i++) game.MoveLeft();
        game.Rotate();
        game.HardDrop();

        var occupied = 0;
        for (var y = 0; y < TetrisGame.Height; y++)
            for (var x = 0; x < TetrisGame.Width; x++)
                if (game.CellAt(x, y) != 0) occupied++;

        Assert.True(occupied >= 8);
        Assert.True(game.Score >= 4);
    }

    [Fact]
    public void Reset_ClearsScoreLinesAndGameOver()
    {
        var game = new TetrisGame(seed: 3);
        game.HardDrop();
        game.Reset();

        Assert.Equal(0, game.Score);
        Assert.Equal(0, game.Lines);
        Assert.False(game.IsGameOver);
    }
}
