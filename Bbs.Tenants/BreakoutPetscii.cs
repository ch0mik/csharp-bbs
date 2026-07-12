using Bbs.Tenants.Content.Games;
using Bbs.Terminals;

namespace Bbs.Tenants;

public sealed class BreakoutPetscii : PetsciiThread
{
    private static readonly TimeSpan FrameDelay = TimeSpan.FromMilliseconds(110);
    private readonly BreakoutGame _game = new();
    private int _highScore = GameHighScores.Best("breakout");
    private bool _scoreRecorded;
    private readonly GameDiagnostics _diagnostics = new("breakout");

    public override async Task DoLoopAsync(CancellationToken cancellationToken = default)
    {
        RenderFull();
        await FlushAsync(cancellationToken).ConfigureAwait(false);

        while (!cancellationToken.IsCancellationRequested)
        {
            if (_game.IsGameOver || _game.HasWon)
            {
                await RecordScoreAsync(cancellationToken).ConfigureAwait(false);
                DrawStatus();
                await FlushAsync(cancellationToken).ConfigureAwait(false);
                var key = await ReadKeyAsync(cancellationToken).ConfigureAwait(false);
                if (IsExit(key))
                {
                    if (await ConfirmExitAsync(cancellationToken).ConfigureAwait(false)) return;
                    RenderFull(); await FlushAsync(cancellationToken).ConfigureAwait(false); continue;
                }

                _game.ResetGame();
                _scoreRecorded = false;
                RenderFull();
                await FlushAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            var previous = CaptureState();
            var input = await KeyPressedAsync(FrameDelay, cancellationToken).ConfigureAwait(false);
            if (IsExit(input))
            {
                if (await ConfirmExitAsync(cancellationToken).ConfigureAwait(false)) return;
                RenderFull(); await FlushAsync(cancellationToken).ConfigureAwait(false); continue;
            }

            HandleInput(input);
            _game.Tick();
            RenderChanges(previous);
            await FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void HandleInput(int key)
    {
        switch (key)
        {
            case 'z':
            case 'Z':
            case PetsciiKeys.Left:
                _game.MoveLeft();
                break;
            case 'x':
            case 'X':
            case PetsciiKeys.Right:
                _game.MoveRight();
                break;
            case PetsciiKeys.Space:
                _game.Launch();
                break;
        }
    }

    private void RenderFull()
    {
        Cls();
        Write(PetsciiKeys.White);
        Print($"SCORE {_game.Score,4}  LIVES {_game.Lives} ");
        Println(_game.HasWon ? "YOU WIN" : _game.IsGameOver ? "GAME OVER" : _game.IsRunning ? "" : "SPACE=GO");

        for (var y = 1; y < BreakoutGame.Height; y++)
        {
            for (var x = 0; x < BreakoutGame.Width; x++)
            {
                if (x == 0 || x == BreakoutGame.Width - 1 || y == 1)
                {
                    Write(PetsciiKeys.Blue, 160);
                    continue;
                }

                var brickRow = y - 2;
                var brickColumn = (x - 1) / 3;
                if (brickRow >= 0 && brickRow < BreakoutGame.BrickRows && _game.HasBrick(brickRow, brickColumn))
                {
                    Write(BrickColor(brickRow), 160);
                }
                else if (x == _game.BallX && y == _game.BallY)
                {
                    Write(PetsciiKeys.Yellow, 'O');
                }
                else if (y == BreakoutGame.PaddleY && x >= _game.PaddleX && x < _game.PaddleX + BreakoutGame.PaddleWidth)
                {
                    const string paddle = "<=====>";
                    Write(PetsciiKeys.LightGreen, paddle[x - _game.PaddleX]);
                }
                else
                {
                    Write(PetsciiKeys.Black, PetsciiKeys.Space);
                }
            }

            Println();
        }

        Write(PetsciiKeys.White);
        if (_game.IsGameOver || _game.HasWon)
        {
            Print("ANY KEY=NEW  Q=EXIT");
        }
        else
        {
            Print("Z/X MOVE  SPACE LAUNCH  Q EXIT");
        }
        ParkCursor();
    }

    private void RenderChanges(RenderState previous)
    {
        var changedCells = 0;
        if (_game.LastBrokenBrick is { } brick)
        {
            DrawSpan(1 + brick.Column * 3, 2 + brick.Row, 3, PetsciiKeys.Black, PetsciiKeys.Space);
            changedCells += 3;
        }

        if (previous.PaddleX != _game.PaddleX)
        {
            DrawSpan(previous.PaddleX, BreakoutGame.PaddleY, BreakoutGame.PaddleWidth, PetsciiKeys.Black, PetsciiKeys.Space);
            DrawPaddle();
            changedCells += BreakoutGame.PaddleWidth * 2;
        }

        if (previous.BallX != _game.BallX || previous.BallY != _game.BallY)
        {
            DrawCell(previous.BallX, previous.BallY, PetsciiKeys.Black, PetsciiKeys.Space);
            DrawCell(_game.BallX, _game.BallY, PetsciiKeys.Yellow, 'O');
            changedCells += 2;
        }

        if (previous.Score != _game.Score || previous.Lives != _game.Lives
            || previous.IsRunning != _game.IsRunning || previous.IsGameOver != _game.IsGameOver
            || previous.HasWon != _game.HasWon)
        {
            DrawStatus();
            _diagnostics.Event($"score:{_game.Score} lives:{_game.Lives} running:{_game.IsRunning}");
        }
        ParkCursor();
        _diagnostics.Frame(changedCells);
    }

    private void DrawStatus()
    {
        PositionCursor(0, 0);
        Write(PetsciiKeys.White);
        var status = $"SCORE {_game.Score,4} L{_game.Lives} ";
        if (GameHighScores.IsAvailable) status += $"HI{_highScore,4} ";
        status += _game.HasWon ? "YOU WIN" : _game.IsGameOver ? "GAME OVER" : _game.IsRunning ? "" : "SPACE=GO";
        Print(status.PadRight(39));
    }

    private void DrawCell(int x, int y, int color, int glyph)
    {
        PositionCursor(x, y);
        Write(color, glyph);
    }

    private void DrawSpan(int x, int y, int length, int color, int glyph)
    {
        PositionCursor(x, y);
        Write(color);
        for (var i = 0; i < length; i++)
        {
            Write(glyph);
        }
    }

    private void DrawPaddle()
    {
        PositionCursor(_game.PaddleX, BreakoutGame.PaddleY);
        Write(PetsciiKeys.LightGreen);
        Print("<=====>");
    }

    private void PositionCursor(int x, int y)
    {
        Write(PetsciiKeys.Home);
        for (var row = 0; row < y; row++) Write(PetsciiKeys.Down);
        for (var column = 0; column < x; column++) Write(PetsciiKeys.Right);
    }

    private void ParkCursor()
    {
        Write(PetsciiKeys.Black);
        PositionCursor(0, 24);
    }

    private async Task<bool> ConfirmExitAsync(CancellationToken token)
    {
        PositionCursor(0, 24);
        Write(PetsciiKeys.White);
        Print("EXIT GAME? Y/N".PadRight(39));
        await FlushAsync(token).ConfigureAwait(false);
        while (true)
        {
            var key = await ReadKeyAsync(token).ConfigureAwait(false);
            if (key is 'y' or 'Y') return true;
            if (key is 'n' or 'N' or 'q' or 'Q') return false;
        }
    }

    private RenderState CaptureState()
        => new(_game.PaddleX, _game.BallX, _game.BallY, _game.Score, _game.Lives,
            _game.IsRunning, _game.IsGameOver, _game.HasWon);

    private async Task RecordScoreAsync(CancellationToken token)
    {
        if (_scoreRecorded) return;
        var player = ClientName;
        if (GameHighScores.IsNewHighScore("breakout", _game.Score))
            player = await ReadHighScoreNameAsync(token).ConfigureAwait(false);
        _highScore = GameHighScores.Submit("breakout", player, _game.Score);
        _scoreRecorded = true;
    }

    private async Task<string> ReadHighScoreNameAsync(CancellationToken token)
    {
        PositionCursor(0, 24); Write(PetsciiKeys.Yellow); Print("NEW HI! NAME (8): ");
        await FlushAsync(token).ConfigureAwait(false);
        var name = (await ReadLineAsync(8, token).ConfigureAwait(false)).Trim();
        PositionCursor(0, 24); Write(PetsciiKeys.Black); Print(" ".PadRight(39));
        return string.IsNullOrWhiteSpace(name) ? ClientName : name;
    }

    private static int BrickColor(int row) => row switch
    {
        0 => PetsciiKeys.Red,
        1 => PetsciiKeys.Orange,
        2 => PetsciiKeys.Yellow,
        3 => PetsciiKeys.Green,
        4 => PetsciiKeys.Cyan,
        _ => PetsciiKeys.Purple
    };

    private static bool IsExit(int key) => key is 'q' or 'Q' or 3;

    private readonly record struct RenderState(
        int PaddleX,
        int BallX,
        int BallY,
        int Score,
        int Lives,
        bool IsRunning,
        bool IsGameOver,
        bool HasWon);
}
