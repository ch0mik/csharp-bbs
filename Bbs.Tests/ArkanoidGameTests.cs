using Bbs.Tenants.Content.Games;

namespace Bbs.Tests;

public class ArkanoidGameTests
{
    [Fact] public void NewGame_HasThreeLivesAndDurableBricks()
    {
        var game = new ArkanoidGame();
        Assert.Equal(3, game.Lives); Assert.Equal(1, game.Level); Assert.True(game.BricksRemaining > 0);
        Assert.Equal(2, game.BrickAt(0, 0)); Assert.False(game.IsRunning);
    }
    [Fact] public void Paddle_StaysInsideWalls()
    {
        var game = new ArkanoidGame();
        for (var i = 0; i < 100; i++) game.MoveLeft(); Assert.Equal(1, game.PaddleX);
        for (var i = 0; i < 100; i++) game.MoveRight(); Assert.Equal(ArkanoidGame.Width - ArkanoidGame.PaddleWidth - 1, game.PaddleX);
    }
    [Fact] public void LaunchedBall_EventuallyHitsABrick()
    {
        var game = new ArkanoidGame(); game.Launch();
        for (var i = 0; i < 500 && game.Score == 0; i++) game.Tick();
        Assert.True(game.Score > 0); Assert.NotNull(game.ChangedBrick);
    }
}
