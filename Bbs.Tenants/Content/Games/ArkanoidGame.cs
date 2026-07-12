namespace Bbs.Tenants.Content.Games;

internal sealed class ArkanoidGame
{
    internal const int Width = 39, Height = 23, PaddleWidth = 5, PaddleY = 22, BrickRows = 6, BrickColumns = 12;
    private readonly int[,] _bricks = new int[BrickRows, BrickColumns];
    private int _dx = 1, _dy = -1;

    public ArkanoidGame() => ResetGame();
    public int PaddleX { get; private set; }
    public int BallX { get; private set; }
    public int BallY { get; private set; }
    public int Score { get; private set; }
    public int Lives { get; private set; }
    public int Level { get; private set; }
    public int BricksRemaining { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsGameOver { get; private set; }
    public bool HasWon => Level > 3;
    public (int Row, int Column)? ChangedBrick { get; private set; }

    public int BrickAt(int row, int column) => row >= 0 && row < BrickRows && column >= 0 && column < BrickColumns ? _bricks[row, column] : 0;
    public void MoveLeft() { PaddleX = Math.Max(1, PaddleX - 2); AttachBall(); }
    public void MoveRight() { PaddleX = Math.Min(Width - PaddleWidth - 1, PaddleX + 2); AttachBall(); }
    public void Launch() { if (!IsGameOver && !HasWon) IsRunning = true; }

    public void Tick()
    {
        ChangedBrick = null;
        if (!IsRunning || IsGameOver || HasWon) return;
        var nx = BallX + _dx; var ny = BallY + _dy;
        if (nx <= 0 || nx >= Width - 1) { _dx = -_dx; nx = BallX + _dx; }
        if (ny <= 1) { _dy = 1; ny = BallY + _dy; }
        var row = ny - 2; var column = (nx - 1) / 3;
        if (row >= 0 && row < BrickRows && column >= 0 && column < BrickColumns && _bricks[row, column] > 0)
        {
            _bricks[row, column]--; ChangedBrick = (row, column); Score += _bricks[row, column] == 0 ? 25 : 10;
            if (_bricks[row, column] == 0) BricksRemaining--;
            _dy = -_dy; ny = BallY + _dy;
            if (BricksRemaining == 0) { Level++; IsRunning = false; if (!HasWon) { LoadLevel(); ResetBall(); } return; }
        }
        if (_dy > 0 && ny >= PaddleY && nx >= PaddleX && nx < PaddleX + PaddleWidth)
        {
            _dy = -1; _dx = nx < PaddleX + PaddleWidth / 2 ? -1 : 1; ny = PaddleY - 1;
        }
        if (ny >= Height) { LoseBall(); return; }
        BallX = nx; BallY = ny;
    }

    public void ResetGame() { Score = 0; Lives = 3; Level = 1; IsGameOver = false; LoadLevel(); ResetBall(); }
    private void LoadLevel()
    {
        BricksRemaining = 0;
        for (var r = 0; r < BrickRows; r++) for (var c = 0; c < BrickColumns; c++)
        {
            var value = Level switch
            {
                1 => r < 2 ? 2 : 1,
                2 => (r + c) % 3 == 0 ? 0 : (c % 4 == 0 ? 2 : 1),
                _ => Math.Abs(c - 5) <= r ? 2 : (r % 2 == 0 ? 1 : 0)
            };
            _bricks[r, c] = value; if (value > 0) BricksRemaining++;
        }
    }
    private void LoseBall() { if (--Lives <= 0) { IsGameOver = true; IsRunning = false; } else ResetBall(); }
    private void ResetBall() { PaddleX = (Width - PaddleWidth) / 2; BallX = PaddleX + PaddleWidth / 2; BallY = PaddleY - 1; _dx = 1; _dy = -1; IsRunning = false; }
    private void AttachBall() { if (!IsRunning) BallX = PaddleX + PaddleWidth / 2; }
}
