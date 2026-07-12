using Bbs.Tenants.Content.Games;

namespace Bbs.Tests;

public class SquashGameTests
{
    [Fact] public void NewGame_HasThreeLivesAndWaitsForServe()
    {
        var game = new SquashGame();
        Assert.Equal(3, game.Lives); Assert.Equal(0, game.Score); Assert.False(game.IsRunning);
    }
    [Fact] public void Paddle_StaysInsidePlayfield()
    {
        var game = new SquashGame();
        for (var i = 0; i < 100; i++) game.MoveUp();
        Assert.Equal(SquashGame.Top, game.PaddleY);
        for (var i = 0; i < 100; i++) game.MoveDown();
        Assert.Equal(SquashGame.Bottom - SquashGame.PaddleHeight + 1, game.PaddleY);
    }
    [Fact] public void ServedBall_MovesAcrossCourt()
    {
        var game = new SquashGame(); var x = game.BallX; game.Launch(); game.Tick(); Assert.NotEqual(x, game.BallX);
    }
}
