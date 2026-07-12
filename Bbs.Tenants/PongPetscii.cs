using Bbs.Tenants.Content.Games;
using Bbs.Terminals;

namespace Bbs.Tenants;

public sealed class PongPetscii : PetsciiThread
{
    private readonly PongGame _game = new();
    private int _highScore = GameHighScores.Best("pong");
    private bool _scoreRecorded;
    private readonly GameDiagnostics _diagnostics = new("pong");

    public override async Task DoLoopAsync(CancellationToken token = default)
    {
        RenderFull(); await FlushAsync(token).ConfigureAwait(false);
        while (!token.IsCancellationRequested)
        {
            if (_game.IsGameOver)
            {
                await RecordScoreAsync(token).ConfigureAwait(false); DrawStatus(); await FlushAsync(token).ConfigureAwait(false);
                var key = await ReadKeyAsync(token).ConfigureAwait(false);
                if (IsExit(key) && await ConfirmExitAsync(token).ConfigureAwait(false)) return;
                _game.StartNextSet(); _scoreRecorded = false; RenderFull(); await FlushAsync(token).ConfigureAwait(false); continue;
            }
            var old = Capture();
            var keyPressed = await KeyPressedAsync(TimeSpan.FromMilliseconds(105), token).ConfigureAwait(false);
            if (IsExit(keyPressed))
            {
                if (await ConfirmExitAsync(token).ConfigureAwait(false)) return;
                RenderFull(); await FlushAsync(token).ConfigureAwait(false); continue;
            }
            if (keyPressed is 'a' or 'A' or PetsciiKeys.Up) _game.MoveUp();
            else if (keyPressed is 'z' or 'Z' or PetsciiKeys.Down) _game.MoveDown();
            else if (keyPressed == PetsciiKeys.Space) _game.Launch();
            _game.Tick(); RenderChanges(old); await FlushAsync(token).ConfigureAwait(false);
        }
    }

    private void RenderFull()
    {
        Cls(); DrawStatus();
        for (var y = 1; y <= PongGame.Bottom + 1; y++)
        {
            Position(0, y);
            for (var x = 0; x < PongGame.Width; x++)
            {
                if (y == 1 || y == PongGame.Bottom + 1) Write(PetsciiKeys.Blue, '#');
                else if (x == PongGame.PlayerX && InPaddle(_game.PlayerY, y)) Write(PetsciiKeys.LightGreen, ']');
                else if (x == PongGame.CpuX && InPaddle(_game.CpuY, y)) Write(PetsciiKeys.LightRed, '[');
                else if (x == _game.BallX && y == _game.BallY) Write(PetsciiKeys.Yellow, 'O');
                else if (x == PongGame.Width / 2 && (y & 1) == 0) Write(PetsciiKeys.Gray, ':');
                else Write(PetsciiKeys.Black, ' ');
            }
        }
        Position(0, 23); Write(PetsciiKeys.White); Print("A/Z MOVE  SPACE SERVE  Q EXIT"); ParkCursor();
    }

    private void RenderChanges(State old)
    {
        var changedCells = 0;
        if (old.PlayerY != _game.PlayerY) { RedrawPaddle(PongGame.PlayerX, old.PlayerY, _game.PlayerY, PetsciiKeys.LightGreen, ']'); changedCells += PongGame.PaddleHeight * 2; }
        if (old.CpuY != _game.CpuY) { RedrawPaddle(PongGame.CpuX, old.CpuY, _game.CpuY, PetsciiKeys.LightRed, '['); changedCells += PongGame.PaddleHeight * 2; }
        if (old.BallX != _game.BallX || old.BallY != _game.BallY)
        {
            Draw(old.BallX, old.BallY, PetsciiKeys.Black, BackgroundAt(old.BallX, old.BallY));
            Draw(_game.BallX, _game.BallY, PetsciiKeys.Yellow, 'O');
            changedCells += 2;
        }
        if (old.PlayerScore != _game.PlayerScore || old.CpuScore != _game.CpuScore || old.TotalPoints != _game.TotalPoints || old.Running != _game.IsRunning)
        {
            DrawStatus();
            if (old.PlayerScore != _game.PlayerScore || old.CpuScore != _game.CpuScore) _diagnostics.Event($"set_score:{_game.PlayerScore}:{_game.CpuScore} total:{_game.TotalPoints}");
        }
        ParkCursor();
        _diagnostics.Frame(changedCells);
    }

    private void RedrawPaddle(int x, int oldY, int newY, int color, int glyph)
    {
        for (var y = oldY; y < oldY + PongGame.PaddleHeight; y++) Draw(x, y, PetsciiKeys.Black, BackgroundAt(x, y));
        for (var y = newY; y < newY + PongGame.PaddleHeight; y++) Draw(x, y, color, glyph);
    }
    private int BackgroundAt(int x, int y) => x == PongGame.Width / 2 && (y & 1) == 0 ? ':' : ' ';
    private static bool InPaddle(int top, int y) => y >= top && y < top + PongGame.PaddleHeight;
    private void DrawStatus()
    {
        Position(0, 0); Write(PetsciiKeys.White);
        var state = _game.IsGameOver ? (_game.PlayerScore > _game.CpuScore ? "YOU WIN" : "CPU WINS") : _game.IsRunning ? "" : "SPACE=GO";
        var text = $"PONG {_game.PlayerScore}:{_game.CpuScore} PTS{_game.TotalPoints} ";
        if (GameHighScores.IsAvailable) text += $"HI{_highScore} ";
        Print((text + state).PadRight(39));
    }
    private async Task<bool> ConfirmExitAsync(CancellationToken token)
    {
        Position(0, 24); Write(PetsciiKeys.White); Print("EXIT GAME? Y/N".PadRight(39)); await FlushAsync(token).ConfigureAwait(false);
        while (true) { var key = await ReadKeyAsync(token).ConfigureAwait(false); if (key is 'y' or 'Y') return true; if (key is 'n' or 'N' or 'q' or 'Q') return false; }
    }
    private void Draw(int x, int y, int color, int glyph) { Position(x, y); Write(color, glyph); }
    private void ParkCursor() { Write(PetsciiKeys.Black); Position(0, 24); }
    private void Position(int x, int y) { Write(PetsciiKeys.Home); for (var r = 0; r < y; r++) Write(PetsciiKeys.Down); for (var c = 0; c < x; c++) Write(PetsciiKeys.Right); }
    private State Capture() => new(_game.PlayerY, _game.CpuY, _game.BallX, _game.BallY, _game.PlayerScore, _game.CpuScore, _game.TotalPoints, _game.IsRunning);
    private async Task RecordScoreAsync(CancellationToken token)
    {
        if (_scoreRecorded) return;
        var score = _game.TotalPoints;
        var player = ClientName;
        if (GameHighScores.IsNewHighScore("pong", score))
        {
            Position(0, 24); Write(PetsciiKeys.Yellow); Print("NEW HI! NAME (8): "); await FlushAsync(token).ConfigureAwait(false);
            var name = (await ReadLineAsync(8, token).ConfigureAwait(false)).Trim(); if (!string.IsNullOrWhiteSpace(name)) player = name;
            Position(0, 24); Write(PetsciiKeys.Black); Print(" ".PadRight(39));
        }
        _highScore = GameHighScores.Submit("pong", player, score); _scoreRecorded = true;
    }
    private static bool IsExit(int key) => key is 'q' or 'Q' or 3;
    private readonly record struct State(int PlayerY, int CpuY, int BallX, int BallY, int PlayerScore, int CpuScore, int TotalPoints, bool Running);
}
