using Bbs.Tenants.Content.Games;
using Bbs.Terminals;

namespace Bbs.Tenants;

public sealed class TetrisPetscii : PetsciiThread
{
    private static readonly TimeSpan FrameDelay = TimeSpan.FromMilliseconds(140);
    private readonly TetrisGame _game = new();
    private int _gravityFrame;
    private int _highScore = GameHighScores.Best("tetris");
    private bool _scoreRecorded;
    private readonly GameDiagnostics _diagnostics = new("tetris");

    public override async Task DoLoopAsync(CancellationToken token = default)
    {
        RenderFull(); await FlushAsync(token).ConfigureAwait(false);
        while (!token.IsCancellationRequested)
        {
            if (_game.IsGameOver)
            {
                await RecordScoreAsync(token).ConfigureAwait(false); DrawPanel(); await FlushAsync(token).ConfigureAwait(false);
                var endKey = await ReadKeyAsync(token).ConfigureAwait(false);
                if (IsExit(endKey) && await ConfirmExitAsync(token).ConfigureAwait(false)) return;
                _game.Reset(); _gravityFrame = 0; _scoreRecorded = false; RenderFull(); await FlushAsync(token).ConfigureAwait(false); continue;
            }

            var old = Capture();
            var key = await KeyPressedAsync(FrameDelay, token).ConfigureAwait(false);
            if (IsExit(key))
            {
                if (await ConfirmExitAsync(token).ConfigureAwait(false)) return;
                RenderFull(); await FlushAsync(token).ConfigureAwait(false); continue;
            }
            HandleInput(key);
            if (++_gravityFrame >= Math.Max(1, 5 - _game.Lines / 10)) { _game.StepDown(); _gravityFrame = 0; }
            RenderChanges(old); await FlushAsync(token).ConfigureAwait(false);
        }
    }

    private void HandleInput(int key)
    {
        switch (key)
        {
            case 'a': case 'A': case PetsciiKeys.Left: _game.MoveLeft(); break;
            case 'd': case 'D': case PetsciiKeys.Right: _game.MoveRight(); break;
            case 'w': case 'W': case PetsciiKeys.Up: _game.Rotate(); break;
            case 's': case 'S': case PetsciiKeys.Down: _game.StepDown(); break;
            case PetsciiKeys.Space: _game.HardDrop(); break;
        }
    }

    private void RenderFull()
    {
        Cls(); Position(0, 0); Write(PetsciiKeys.Cyan); Print("TETRIS PETSCII");
        Position(0, 1); Write(PetsciiKeys.White); Print("+----------+  SCORE");
        for (var y = 0; y < TetrisGame.Height; y++)
        {
            Position(0, y + 2); Write(PetsciiKeys.White); Print("|");
            for (var x = 0; x < TetrisGame.Width; x++) DrawCell(x, y, _game.CellAt(x, y));
            Position(11, y + 2); Write(PetsciiKeys.White); Print("|");
        }
        Position(0, 22); Write(PetsciiKeys.White); Print("+----------+");
        DrawPanel(); ParkCursor();
    }

    private void RenderChanges(Snapshot old)
    {
        var changedCells = 0;
        for (var y = 0; y < TetrisGame.Height; y++)
            for (var x = 0; x < TetrisGame.Width; x++)
            {
                var cell = _game.CellAt(x, y);
                if (old.Cells[y * TetrisGame.Width + x] != cell) { DrawCell(x, y, cell); changedCells++; }
            }
        if (old.Score != _game.Score || old.Lines != _game.Lines || old.GameOver != _game.IsGameOver)
        {
            DrawPanel();
            if (old.Lines != _game.Lines || old.GameOver != _game.IsGameOver) _diagnostics.Event($"score:{_game.Score} lines:{_game.Lines} game_over:{_game.IsGameOver}");
        }
        ParkCursor();
        _diagnostics.Frame(changedCells);
    }

    private void DrawCell(int x, int y, int cell)
    {
        Position(x + 1, y + 2);
        if (cell == 0) Write(PetsciiKeys.Black, ' '); else Write(PieceColor(cell), '#');
    }

    private void DrawPanel()
    {
        WriteTextAt(14, 2, $"{_game.Score,6}");
        WriteTextAt(14, 3, GameHighScores.IsAvailable ? $"HI {_highScore,6}" : "".PadRight(12));
        WriteTextAt(14, 4, "LINES"); WriteTextAt(14, 5, $"{_game.Lines,6}");
        WriteTextAt(14, 8, "A/D MOVE"); WriteTextAt(14, 9, "W ROTATE");
        WriteTextAt(14, 10, "S DOWN"); WriteTextAt(14, 11, "SPACE DROP"); WriteTextAt(14, 12, "Q EXIT");
        WriteTextAt(14, 15, (_game.IsGameOver ? "GAME OVER" : "").PadRight(12));
    }

    private void WriteTextAt(int x, int y, string text) { Position(x, y); Write(PetsciiKeys.White); Print(text); }
    private async Task<bool> ConfirmExitAsync(CancellationToken token)
    {
        WriteTextAt(0, 24, "EXIT GAME? Y/N".PadRight(39)); await FlushAsync(token).ConfigureAwait(false);
        while (true) { var key = await ReadKeyAsync(token).ConfigureAwait(false); if (key is 'y' or 'Y') return true; if (key is 'n' or 'N' or 'q' or 'Q') return false; }
    }
    private Snapshot Capture()
    {
        var cells = new int[TetrisGame.Width * TetrisGame.Height];
        for (var y = 0; y < TetrisGame.Height; y++) for (var x = 0; x < TetrisGame.Width; x++) cells[y * TetrisGame.Width + x] = _game.CellAt(x, y);
        return new(cells, _game.Score, _game.Lines, _game.IsGameOver);
    }
    private async Task RecordScoreAsync(CancellationToken token)
    {
        if (_scoreRecorded) return;
        var player = ClientName;
        if (GameHighScores.IsNewHighScore("tetris", _game.Score))
        {
            WriteTextAt(0, 24, "NEW HI! NAME (8): "); await FlushAsync(token).ConfigureAwait(false);
            var name = (await ReadLineAsync(8, token).ConfigureAwait(false)).Trim();
            if (!string.IsNullOrWhiteSpace(name)) player = name;
            WriteTextAt(0, 24, " ".PadRight(39));
        }
        _highScore = GameHighScores.Submit("tetris", player, _game.Score); _scoreRecorded = true;
    }
    private void ParkCursor() { Write(PetsciiKeys.Black); Position(0, 24); }
    private void Position(int x, int y) { Write(PetsciiKeys.Home); for (var r = 0; r < y; r++) Write(PetsciiKeys.Down); for (var c = 0; c < x; c++) Write(PetsciiKeys.Right); }
    private static int PieceColor(int p) => p switch { 1 => PetsciiKeys.Cyan, 2 => PetsciiKeys.Yellow, 3 => PetsciiKeys.Green, 4 => PetsciiKeys.Red, 5 => PetsciiKeys.Purple, 6 => PetsciiKeys.Orange, _ => PetsciiKeys.LightBlue };
    private static bool IsExit(int key) => key is 'q' or 'Q' or 3;
    private sealed record Snapshot(int[] Cells, int Score, int Lines, bool GameOver);
}
