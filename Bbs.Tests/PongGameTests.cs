using Bbs.Tenants.Content.Games;

namespace Bbs.Tests;

public class PongGameTests
{
    [Fact] public void Match_StartsAtZeroAndWaitsForServe()
    {
        var game = new PongGame();
        Assert.Equal(0, game.PlayerScore); Assert.Equal(0, game.CpuScore); Assert.False(game.IsRunning);
    }
    [Fact] public void PlayerPaddle_StaysInsideCourt()
    {
        var game = new PongGame();
        for (var i = 0; i < 100; i++) game.MoveUp(); Assert.Equal(PongGame.Top, game.PlayerY);
        for (var i = 0; i < 100; i++) game.MoveDown(); Assert.Equal(PongGame.Bottom - PongGame.PaddleHeight + 1, game.PlayerY);
    }
    [Fact] public void Serve_StartsMovingBallAndCpu()
    {
        var game = new PongGame(); var x = game.BallX;
        game.Launch(); game.Tick();
        Assert.True(game.IsRunning); Assert.NotEqual(x, game.BallX);
    }

    [Fact] public void StartingNextSet_PreservesTotalPoints()
    {
        var game = new PongGame();
        game.Launch();
        for (var i = 0; i < 500 && game.TotalPoints == 0; i++) game.Tick();
        var total = game.TotalPoints;
        Assert.True(total > 0);
        game.StartNextSet();
        Assert.Equal(total, game.TotalPoints);
        Assert.Equal(0, game.PlayerScore);
        Assert.Equal(0, game.CpuScore);
    }
}
