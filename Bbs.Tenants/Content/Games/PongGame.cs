namespace Bbs.Tenants.Content.Games;

internal sealed class PongGame
{
    internal const int Width = 39, Top = 2, Bottom = 21, PaddleHeight = 5;
    internal const int PlayerX = 1, CpuX = Width - 2;
    private int _dx = 1, _dy = -1, _aiTick;

    public PongGame() => ResetMatch();
    public int PlayerY { get; private set; }
    public int CpuY { get; private set; }
    public int BallX { get; private set; }
    public int BallY { get; private set; }
    public int PlayerScore { get; private set; }
    public int CpuScore { get; private set; }
    public int TotalPoints { get; private set; }
    public int PlayerSets { get; private set; }
    public int CpuSets { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsGameOver => PlayerScore >= 7 || CpuScore >= 7;

    public void MoveUp() => PlayerY = Math.Max(Top, PlayerY - 2);
    public void MoveDown() => PlayerY = Math.Min(Bottom - PaddleHeight + 1, PlayerY + 2);
    public void Launch() { if (!IsGameOver) IsRunning = true; }

    public void Tick()
    {
        if (!IsRunning || IsGameOver) return;
        MoveCpu();
        var nx = BallX + _dx;
        var ny = BallY + _dy;
        if (ny <= Top || ny >= Bottom) { _dy = -_dy; ny = BallY + _dy; }
        if (_dx < 0 && nx <= PlayerX + 1 && Hits(PlayerY, ny)) { _dx = 1; nx = PlayerX + 2; AimFrom(PlayerY, ny); TotalPoints++; }
        if (_dx > 0 && nx >= CpuX - 1 && Hits(CpuY, ny)) { _dx = -1; nx = CpuX - 2; AimFrom(CpuY, ny); }
        if (nx <= 0) { CpuScore++; ResetBall(direction: 1); return; }
        if (nx >= Width - 1) { PlayerScore++; TotalPoints += 10; ResetBall(direction: -1); return; }
        BallX = nx; BallY = ny;
    }

    public void ResetMatch()
    {
        PlayerScore = CpuScore = TotalPoints = PlayerSets = CpuSets = 0;
        PlayerY = CpuY = 9; ResetBall(1);
    }

    public void StartNextSet()
    {
        if (PlayerScore >= 7) PlayerSets++;
        else if (CpuScore >= 7) CpuSets++;
        PlayerScore = CpuScore = 0;
        PlayerY = CpuY = 9;
        ResetBall((PlayerSets + CpuSets) % 2 == 0 ? 1 : -1);
    }
    private bool Hits(int paddleY, int y) => y >= paddleY && y < paddleY + PaddleHeight;
    private void AimFrom(int paddleY, int y) => _dy = y < paddleY + PaddleHeight / 2 ? -1 : 1;
    private void MoveCpu()
    {
        if (++_aiTick % 2 != 0) return;
        var center = CpuY + PaddleHeight / 2;
        if (BallY < center) CpuY = Math.Max(Top, CpuY - 1);
        else if (BallY > center) CpuY = Math.Min(Bottom - PaddleHeight + 1, CpuY + 1);
    }
    private void ResetBall(int direction)
    {
        BallX = Width / 2; BallY = (Top + Bottom) / 2; _dx = direction; _dy = -1; _aiTick = 0; IsRunning = false;
    }
}
