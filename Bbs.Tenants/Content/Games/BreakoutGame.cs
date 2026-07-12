namespace Bbs.Tenants.Content.Games;

internal sealed class BreakoutGame
{
    internal const int Width = 39;
    internal const int Height = 23;
    internal const int PaddleWidth = 7;
    internal const int PaddleY = Height - 1;
    internal const int BrickRows = 6;
    internal const int BrickColumns = 12;

    private readonly bool[,] _bricks = new bool[BrickRows, BrickColumns];
    private int _ballDx = 1;
    private int _ballDy = -1;

    public BreakoutGame() => ResetGame();

    public int PaddleX { get; private set; }
    public int BallX { get; private set; }
    public int BallY { get; private set; }
    public int Lives { get; private set; }
    public int Score { get; private set; }
    public int BricksRemaining { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsGameOver { get; private set; }
    public bool HasWon => BricksRemaining == 0;
    public (int Row, int Column)? LastBrokenBrick { get; private set; }

    public bool HasBrick(int row, int column)
        => row >= 0 && row < BrickRows && column >= 0 && column < BrickColumns && _bricks[row, column];

    public void MoveLeft()
    {
        PaddleX = Math.Max(1, PaddleX - 2);
        AttachBallToPaddleIfStopped();
    }

    public void MoveRight()
    {
        PaddleX = Math.Min(Width - PaddleWidth - 1, PaddleX + 2);
        AttachBallToPaddleIfStopped();
    }

    public void Launch()
    {
        if (!IsGameOver && !HasWon)
        {
            IsRunning = true;
        }
    }

    public void Tick()
    {
        LastBrokenBrick = null;
        if (!IsRunning || IsGameOver || HasWon)
        {
            return;
        }

        var nextX = BallX + _ballDx;
        var nextY = BallY + _ballDy;

        if (nextX <= 0 || nextX >= Width - 1)
        {
            _ballDx = -_ballDx;
            nextX = BallX + _ballDx;
        }

        if (nextY <= 1)
        {
            _ballDy = 1;
            nextY = BallY + _ballDy;
        }

        if (TryHitBrick(nextX, nextY))
        {
            _ballDy = -_ballDy;
            nextY = BallY + _ballDy;
        }

        if (_ballDy > 0 && nextY >= PaddleY && nextX >= PaddleX && nextX < PaddleX + PaddleWidth)
        {
            _ballDy = -1;
            var paddleCenter = PaddleX + PaddleWidth / 2;
            _ballDx = nextX < paddleCenter ? -1 : 1;
            nextY = PaddleY - 1;
        }

        if (nextY >= Height)
        {
            LoseBall();
            return;
        }

        BallX = nextX;
        BallY = nextY;
    }

    public void ResetGame()
    {
        Lives = 3;
        Score = 0;
        IsGameOver = false;
        BricksRemaining = BrickRows * BrickColumns;
        for (var row = 0; row < BrickRows; row++)
        {
            for (var column = 0; column < BrickColumns; column++)
            {
                _bricks[row, column] = true;
            }
        }

        ResetBall();
    }

    private bool TryHitBrick(int x, int y)
    {
        var row = y - 2;
        var column = (x - 1) / 3;
        if (row < 0 || row >= BrickRows || column < 0 || column >= BrickColumns || !_bricks[row, column])
        {
            return false;
        }

        _bricks[row, column] = false;
        LastBrokenBrick = (row, column);
        BricksRemaining--;
        Score += 10;
        if (BricksRemaining == 0)
        {
            IsRunning = false;
        }

        return true;
    }

    private void LoseBall()
    {
        Lives--;
        if (Lives <= 0)
        {
            IsGameOver = true;
            IsRunning = false;
            return;
        }

        ResetBall();
    }

    private void ResetBall()
    {
        PaddleX = (Width - PaddleWidth) / 2;
        _ballDx = 1;
        _ballDy = -1;
        IsRunning = false;
        AttachBallToPaddleIfStopped();
    }

    private void AttachBallToPaddleIfStopped()
    {
        if (!IsRunning)
        {
            BallX = PaddleX + PaddleWidth / 2;
            BallY = PaddleY - 1;
        }
    }
}
