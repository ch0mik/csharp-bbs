using Bbs.Tenants.Content.Games;
using Bbs.Terminals;

namespace Bbs.Tenants;

public sealed class ArkanoidPetscii : PetsciiThread
{
    private readonly ArkanoidGame _game = new();
    private readonly GameDiagnostics _diagnostics = new("arkanoid");
    private int _highScore = GameHighScores.Best("arkanoid");
    private bool _recorded;

    public override async Task DoLoopAsync(CancellationToken token = default)
    {
        RenderFull(); await FlushAsync(token).ConfigureAwait(false);
        while (!token.IsCancellationRequested)
        {
            if (_game.IsGameOver || _game.HasWon)
            {
                await RecordScoreAsync(token).ConfigureAwait(false); DrawStatus(); await FlushAsync(token).ConfigureAwait(false);
                var end = await ReadKeyAsync(token).ConfigureAwait(false);
                if (IsExit(end) && await ConfirmExitAsync(token).ConfigureAwait(false)) return;
                _game.ResetGame(); _recorded = false; RenderFull(); await FlushAsync(token).ConfigureAwait(false); continue;
            }
            var old = Capture();
            var key = await KeyPressedAsync(TimeSpan.FromMilliseconds(Math.Max(70, 115 - (_game.Level - 1) * 15)), token).ConfigureAwait(false);
            if (IsExit(key)) { if (await ConfirmExitAsync(token).ConfigureAwait(false)) return; RenderFull(); await FlushAsync(token).ConfigureAwait(false); continue; }
            if (key is 'z' or 'Z' or PetsciiKeys.Left) _game.MoveLeft();
            else if (key is 'x' or 'X' or PetsciiKeys.Right) _game.MoveRight();
            else if (key == PetsciiKeys.Space) _game.Launch();
            _game.Tick();
            if (old.Level != _game.Level && !_game.HasWon) RenderFull(); else RenderChanges(old);
            await FlushAsync(token).ConfigureAwait(false);
        }
    }

    private void RenderFull()
    {
        Cls(); DrawStatus();
        for (var y = 1; y < ArkanoidGame.Height; y++)
        {
            Position(0, y);
            for (var x = 0; x < ArkanoidGame.Width; x++)
            {
                var row = y - 2; var col = (x - 1) / 3;
                if (x == 0 || x == ArkanoidGame.Width - 1 || y == 1) Write(PetsciiKeys.Blue, '#');
                else if (row >= 0 && row < ArkanoidGame.BrickRows && _game.BrickAt(row, col) > 0) Write(BrickColor(row, _game.BrickAt(row, col)), _game.BrickAt(row, col) > 1 ? '@' : '#');
                else if (x == _game.BallX && y == _game.BallY) Write(PetsciiKeys.Yellow, 'O');
                else if (y == ArkanoidGame.PaddleY && x >= _game.PaddleX && x < _game.PaddleX + ArkanoidGame.PaddleWidth) Write(PetsciiKeys.LightGreen, "<===>"[x - _game.PaddleX]);
                else Write(PetsciiKeys.Black, ' ');
            }
        }
        Position(0, 23); Write(PetsciiKeys.White); Print("Z/X MOVE SPACE LAUNCH Q EXIT"); Park();
    }

    private void RenderChanges(State old)
    {
        var changed = 0;
        if (_game.ChangedBrick is { } b) { DrawBrick(b.Row, b.Column); changed += 3; }
        if (old.PaddleX != _game.PaddleX) { DrawSpan(old.PaddleX, ArkanoidGame.PaddleY, "     ", PetsciiKeys.Black); DrawSpan(_game.PaddleX, ArkanoidGame.PaddleY, "<===>", PetsciiKeys.LightGreen); changed += 10; }
        if (old.BallX != _game.BallX || old.BallY != _game.BallY) { Draw(old.BallX, old.BallY, PetsciiKeys.Black, ' '); Draw(_game.BallX, _game.BallY, PetsciiKeys.Yellow, 'O'); changed += 2; }
        if (old.Score != _game.Score || old.Lives != _game.Lives || old.Running != _game.IsRunning) { DrawStatus(); _diagnostics.Event($"score:{_game.Score} level:{_game.Level} lives:{_game.Lives}"); }
        Park(); _diagnostics.Frame(changed);
    }

    private void DrawBrick(int row, int col)
    {
        var hp = _game.BrickAt(row, col); Position(1 + col * 3, 2 + row);
        Write(hp == 0 ? PetsciiKeys.Black : BrickColor(row, hp));
        for (var i = 0; i < 3; i++) Write(hp == 0 ? ' ' : hp > 1 ? '@' : '#');
    }
    private void DrawStatus()
    {
        Position(0, 0); Write(PetsciiKeys.White); var text = $"ARK L{_game.Level} S{_game.Score} V{_game.Lives} ";
        if (GameHighScores.IsAvailable) text += $"HI{_highScore} ";
        text += _game.HasWon ? "YOU WIN" : _game.IsGameOver ? "GAME OVER" : _game.IsRunning ? "" : "SPACE=GO"; Print(text.PadRight(39));
    }
    private async Task RecordScoreAsync(CancellationToken token)
    {
        if (_recorded) return; var name = ClientName;
        if (GameHighScores.IsNewHighScore("arkanoid", _game.Score)) { Position(0, 24); Write(PetsciiKeys.Yellow); Print("NEW HI! NAME (8): "); await FlushAsync(token).ConfigureAwait(false); var entered = (await ReadLineAsync(8, token).ConfigureAwait(false)).Trim(); if (entered.Length > 0) name = entered; }
        _highScore = GameHighScores.Submit("arkanoid", name, _game.Score); _recorded = true;
    }
    private async Task<bool> ConfirmExitAsync(CancellationToken token)
    {
        Position(0, 24); Write(PetsciiKeys.White); Print("EXIT GAME? Y/N".PadRight(39)); await FlushAsync(token).ConfigureAwait(false);
        while (true) { var key = await ReadKeyAsync(token).ConfigureAwait(false); if (key is 'y' or 'Y') return true; if (key is 'n' or 'N' or 'q' or 'Q') return false; }
    }
    private void Draw(int x, int y, int color, int glyph) { Position(x, y); Write(color, glyph); }
    private void DrawSpan(int x, int y, string text, int color) { Position(x, y); Write(color); Print(text); }
    private void Position(int x, int y) { Write(PetsciiKeys.Home); for (var r = 0; r < y; r++) Write(PetsciiKeys.Down); for (var c = 0; c < x; c++) Write(PetsciiKeys.Right); }
    private void Park() { Write(PetsciiKeys.Black); Position(0, 24); }
    private State Capture() => new(_game.PaddleX, _game.BallX, _game.BallY, _game.Score, _game.Lives, _game.Level, _game.IsRunning);
    private static int BrickColor(int row, int hp) => hp > 1 ? PetsciiKeys.LightRed : row switch { 0 => PetsciiKeys.Red, 1 => PetsciiKeys.Orange, 2 => PetsciiKeys.Yellow, 3 => PetsciiKeys.Green, 4 => PetsciiKeys.Cyan, _ => PetsciiKeys.Purple };
    private static bool IsExit(int key) => key is 'q' or 'Q' or 3;
    private readonly record struct State(int PaddleX, int BallX, int BallY, int Score, int Lives, int Level, bool Running);
}
