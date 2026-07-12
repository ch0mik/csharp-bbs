using Bbs.Tenants.Content.Games;
using Bbs.Terminals;

namespace Bbs.Tenants;

public sealed class SquashPetscii : PetsciiThread
{
    private readonly SquashGame _game = new();
    private int _highScore = GameHighScores.Best("squash");
    private bool _scoreRecorded;
    private readonly GameDiagnostics _diagnostics = new("squash");

    public override async Task DoLoopAsync(CancellationToken token = default)
    {
        RenderFull(); await FlushAsync(token).ConfigureAwait(false);
        while (!token.IsCancellationRequested)
        {
            if (_game.IsGameOver)
            {
                await RecordScoreAsync(token).ConfigureAwait(false); DrawStatus(); await FlushAsync(token).ConfigureAwait(false);
                var endKey = await ReadKeyAsync(token).ConfigureAwait(false);
                if (IsExit(endKey))
                {
                    if (await ConfirmExitAsync(token).ConfigureAwait(false)) return;
                    RenderFull(); await FlushAsync(token).ConfigureAwait(false); continue;
                }
                _game.ResetGame(); _scoreRecorded = false; RenderFull(); await FlushAsync(token).ConfigureAwait(false); continue;
            }
            var old = Capture();
            var key = await KeyPressedAsync(TimeSpan.FromMilliseconds(Math.Max(65, 145 - _game.Score * 4)), token).ConfigureAwait(false);
            if (IsExit(key))
            {
                if (await ConfirmExitAsync(token).ConfigureAwait(false)) return;
                RenderFull(); await FlushAsync(token).ConfigureAwait(false); continue;
            }
            if (key is 'a' or 'A' or PetsciiKeys.Up) _game.MoveUp();
            else if (key is 'z' or 'Z' or PetsciiKeys.Down) _game.MoveDown();
            else if (key == PetsciiKeys.Space) _game.Launch();
            _game.Tick(); RenderChanges(old); await FlushAsync(token).ConfigureAwait(false);
        }
    }

    private void RenderFull()
    {
        Cls(); DrawStatus();
        for (var y = 1; y <= SquashGame.Bottom + 1; y++)
        {
            Position(0, y);
            for (var x = 0; x < SquashGame.Width; x++)
            {
                if (y == 1 || y == SquashGame.Bottom + 1 || x == SquashGame.Width - 1) Write(PetsciiKeys.Blue, '#');
                else if (x == SquashGame.PaddleX && y >= _game.PaddleY && y < _game.PaddleY + SquashGame.PaddleHeight) Write(PetsciiKeys.LightGreen, ']');
                else if (x == _game.BallX && y == _game.BallY) Write(PetsciiKeys.Yellow, 'O');
                else Write(PetsciiKeys.Black, PetsciiKeys.Space);
            }
        }
        Position(0, 23);
        Write(PetsciiKeys.White); Print("A/Z MOVE  SPACE SERVE  Q EXIT");
        ParkCursor();
    }

    private void RenderChanges(State old)
    {
        var changedCells = 0;
        if (old.PaddleY != _game.PaddleY)
        {
            for (var y = old.PaddleY; y < old.PaddleY + SquashGame.PaddleHeight; y++) Draw(SquashGame.PaddleX, y, PetsciiKeys.Black, ' ');
            for (var y = _game.PaddleY; y < _game.PaddleY + SquashGame.PaddleHeight; y++) Draw(SquashGame.PaddleX, y, PetsciiKeys.LightGreen, ']');
            changedCells += SquashGame.PaddleHeight * 2;
        }
        if (old.BallX != _game.BallX || old.BallY != _game.BallY)
        {
            Draw(old.BallX, old.BallY, PetsciiKeys.Black, ' '); Draw(_game.BallX, _game.BallY, PetsciiKeys.Yellow, 'O');
            changedCells += 2;
        }
        if (old.Score != _game.Score || old.Lives != _game.Lives || old.Running != _game.IsRunning || old.GameOver != _game.IsGameOver)
        {
            DrawStatus(); _diagnostics.Event($"score:{_game.Score} lives:{_game.Lives} game_over:{_game.IsGameOver}");
        }
        ParkCursor();
        _diagnostics.Frame(changedCells);
    }

    private void DrawStatus()
    {
        Position(0, 0); Write(PetsciiKeys.White);
        var text = $"SQUASH {_game.Score,3} ";
        if (GameHighScores.IsAvailable) text += $"HI{_highScore,3} ";
        text += $"L{_game.Lives} " + (_game.IsGameOver ? "GAME OVER" : _game.IsRunning ? "" : "SPACE=GO");
        Print(text.PadRight(39));
    }
    private void Draw(int x, int y, int color, int glyph) { Position(x, y); Write(color, glyph); }
    private void ParkCursor() { Write(PetsciiKeys.Black); Position(0, 24); }
    private async Task<bool> ConfirmExitAsync(CancellationToken token)
    {
        Position(0, 24);
        Write(PetsciiKeys.White);
        Print("EXIT GAME? Y/N".PadRight(39));
        await FlushAsync(token).ConfigureAwait(false);
        while (!token.IsCancellationRequested)
        {
            var key = await ReadKeyAsync(token).ConfigureAwait(false);
            if (key is 'y' or 'Y') return true;
            if (key is 'n' or 'N' or 'q' or 'Q') return false;
        }
        return true;
    }
    private void Position(int x, int y)
    {
        Write(PetsciiKeys.Home);
        for (var row = 0; row < y; row++) Write(PetsciiKeys.Down);
        for (var column = 0; column < x; column++) Write(PetsciiKeys.Right);
    }
    private State Capture() => new(_game.PaddleY, _game.BallX, _game.BallY, _game.Score, _game.Lives, _game.IsRunning, _game.IsGameOver);
    private async Task RecordScoreAsync(CancellationToken token)
    {
        if (_scoreRecorded) return;
        var player = ClientName;
        if (GameHighScores.IsNewHighScore("squash", _game.Score))
        {
            Position(0, 24); Write(PetsciiKeys.Yellow); Print("NEW HI! NAME (8): "); await FlushAsync(token).ConfigureAwait(false);
            var name = (await ReadLineAsync(8, token).ConfigureAwait(false)).Trim(); if (!string.IsNullOrWhiteSpace(name)) player = name;
            Position(0, 24); Write(PetsciiKeys.Black); Print(" ".PadRight(39));
        }
        _highScore = GameHighScores.Submit("squash", player, _game.Score); _scoreRecorded = true;
    }
    private static bool IsExit(int key) => key is 'q' or 'Q' or 3;
    private readonly record struct State(int PaddleY, int BallX, int BallY, int Score, int Lives, bool Running, bool GameOver);
}
