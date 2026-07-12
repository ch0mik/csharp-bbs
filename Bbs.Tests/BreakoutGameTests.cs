using Bbs.Tenants.Content.Games;

namespace Bbs.Tests;

public class BreakoutGameTests
{
    [Fact]
    public void NewGame_HasC64SizedPlayfieldAndFreshState()
    {
        var game = new BreakoutGame();

        Assert.Equal(39, BreakoutGame.Width);
        Assert.Equal(23, BreakoutGame.Height);
        Assert.Equal(3, game.Lives);
        Assert.Equal(BreakoutGame.BrickRows * BreakoutGame.BrickColumns, game.BricksRemaining);
        Assert.False(game.IsRunning);
        Assert.False(game.IsGameOver);
    }

    [Fact]
    public void Paddle_StaysInsideSideWalls()
    {
        var game = new BreakoutGame();

        for (var i = 0; i < 100; i++) game.MoveLeft();
        Assert.Equal(1, game.PaddleX);
        Assert.Equal(game.PaddleX + BreakoutGame.PaddleWidth / 2, game.BallX);

        for (var i = 0; i < 100; i++) game.MoveRight();
        Assert.Equal(BreakoutGame.Width - BreakoutGame.PaddleWidth - 1, game.PaddleX);
    }

    [Fact]
    public void Launch_StartsBallAndEventuallyBreaksABrick()
    {
        var game = new BreakoutGame();
        var initialBricks = game.BricksRemaining;

        game.Launch();
        for (var i = 0; i < 500 && game.BricksRemaining == initialBricks; i++)
        {
            game.Tick();
        }

        Assert.True(game.IsRunning);
        Assert.True(game.BricksRemaining < initialBricks);
        Assert.True(game.Score > 0);
    }

    [Fact]
    public void ResetGame_RestoresScoreLivesAndBricks()
    {
        var game = new BreakoutGame();
        game.Launch();
        for (var i = 0; i < 100; i++) game.Tick();

        game.ResetGame();

        Assert.Equal(0, game.Score);
        Assert.Equal(3, game.Lives);
        Assert.Equal(BreakoutGame.BrickRows * BreakoutGame.BrickColumns, game.BricksRemaining);
        Assert.False(game.IsRunning);
    }
}
