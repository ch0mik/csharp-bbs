namespace Bbs.Tenants.Content.Games;

internal sealed class SquashGame
{
    internal const int Width = 39;
    internal const int Top = 2;
    internal const int Bottom = 21;
    internal const int PaddleX = 1;
    internal const int PaddleHeight = 5;
    private int _dx = -1, _dy = -1;

    public SquashGame() => ResetGame();
    public int PaddleY { get; private set; }
    public int BallX { get; private set; }
    public int BallY { get; private set; }
    public int Score { get; private set; }
    public int Lives { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsGameOver { get; private set; }

    public void MoveUp() => PaddleY = Math.Max(Top, PaddleY - 2);
    public void MoveDown() => PaddleY = Math.Min(Bottom - PaddleHeight + 1, PaddleY + 2);
    public void Launch() { if (!IsGameOver) IsRunning = true; }

    public void Tick()
    {
        if (!IsRunning || IsGameOver) return;
        var nextX = BallX + _dx;
        var nextY = BallY + _dy;
        if (nextY <= Top || nextY >= Bottom) { _dy = -_dy; nextY = BallY + _dy; }
        if (nextX >= Width - 2) { _dx = -1; nextX = BallX + _dx; }
        if (_dx < 0 && nextX <= PaddleX + 1 && nextY >= PaddleY && nextY < PaddleY + PaddleHeight)
        {
            _dx = 1;
            _dy = nextY < PaddleY + PaddleHeight / 2 ? -1 : 1;
            nextX = PaddleX + 2;
            Score++;
        }
        if (nextX <= 0) { LoseBall(); return; }
        BallX = nextX;
        BallY = nextY;
    }

    public void ResetGame() { Score = 0; Lives = 3; IsGameOver = false; ResetBall(); }
    private void LoseBall()
    {
        if (--Lives <= 0) { IsRunning = false; IsGameOver = true; }
        else ResetBall();
    }
    private void ResetBall()
    {
        PaddleY = (Top + Bottom - PaddleHeight) / 2;
        BallX = Width / 2; BallY = (Top + Bottom) / 2;
        _dx = -1; _dy = -1; IsRunning = false;
    }
}
