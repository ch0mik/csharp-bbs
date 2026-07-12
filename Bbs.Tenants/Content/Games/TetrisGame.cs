namespace Bbs.Tenants.Content.Games;

internal sealed class TetrisGame
{
    internal const int Width = 10;
    internal const int Height = 20;

    private static readonly string[][][] Pieces =
    [
        [["....", "####", "....", "...."], ["..#.", "..#.", "..#.", "..#."]],
        [[".##.", ".##.", "....", "...."]],
        [[".##.", "##..", "....", "...."], [".#..", ".##.", "..#.", "...."]],
        [["##..", ".##.", "....", "...."], ["..#.", ".##.", ".#..", "...."]],
        [[".#..", "###.", "....", "...."], [".#..", ".##.", ".#..", "...."], ["....", "###.", ".#..", "...."], [".#..", "##..", ".#..", "...."]],
        [["#...", "###.", "....", "...."], [".##.", ".#..", ".#..", "...."], ["....", "###.", "..#.", "...."], [".#..", ".#..", "##..", "...."]],
        [["..#.", "###.", "....", "...."], [".#..", ".#..", ".##.", "...."], ["....", "###.", "#...", "...."], ["##..", ".#..", ".#..", "...."]]
    ];

    private readonly int[,] _board = new int[Height, Width];
    private readonly Random _random;
    private int _piece;
    private int _rotation;
    private int _x;
    private int _y;

    public TetrisGame(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : Random.Shared;
        Reset();
    }

    public int Score { get; private set; }
    public int Lines { get; private set; }
    public bool IsGameOver { get; private set; }

    public int CellAt(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return 0;
        var active = ActiveCellAt(x, y);
        return active != 0 ? active : _board[y, x];
    }

    public bool MoveLeft() => TryMove(-1, 0);
    public bool MoveRight() => TryMove(1, 0);

    public bool Rotate()
    {
        if (IsGameOver) return false;
        var next = (_rotation + 1) % Pieces[_piece].Length;
        if (Collides(_x, _y, next)) return false;
        _rotation = next;
        return true;
    }

    public bool StepDown()
    {
        if (IsGameOver) return false;
        if (TryMove(0, 1)) return true;
        LockPiece();
        ClearLines();
        SpawnPiece();
        return false;
    }

    public void HardDrop()
    {
        if (IsGameOver) return;
        var distance = 0;
        while (TryMove(0, 1)) distance++;
        Score += distance * 2;
        LockPiece();
        ClearLines();
        SpawnPiece();
    }

    public void Reset()
    {
        Array.Clear(_board);
        Score = 0;
        Lines = 0;
        IsGameOver = false;
        SpawnPiece();
    }

    private bool TryMove(int dx, int dy)
    {
        if (IsGameOver || Collides(_x + dx, _y + dy, _rotation)) return false;
        _x += dx;
        _y += dy;
        return true;
    }

    private void SpawnPiece()
    {
        _piece = _random.Next(Pieces.Length);
        _rotation = 0;
        _x = 3;
        _y = 0;
        if (Collides(_x, _y, _rotation)) IsGameOver = true;
    }

    private bool Collides(int px, int py, int rotation)
    {
        foreach (var (x, y) in PieceCells(rotation))
        {
            var boardX = px + x;
            var boardY = py + y;
            if (boardX < 0 || boardX >= Width || boardY < 0 || boardY >= Height || _board[boardY, boardX] != 0)
                return true;
        }
        return false;
    }

    private int ActiveCellAt(int boardX, int boardY)
    {
        if (IsGameOver) return 0;
        foreach (var (x, y) in PieceCells(_rotation))
            if (_x + x == boardX && _y + y == boardY) return _piece + 1;
        return 0;
    }

    private IEnumerable<(int X, int Y)> PieceCells(int rotation)
    {
        var shape = Pieces[_piece][rotation];
        for (var y = 0; y < 4; y++)
            for (var x = 0; x < 4; x++)
                if (shape[y][x] == '#') yield return (x, y);
    }

    private void LockPiece()
    {
        foreach (var (x, y) in PieceCells(_rotation))
            _board[_y + y, _x + x] = _piece + 1;
        Score += 4;
    }

    private void ClearLines()
    {
        var cleared = 0;
        for (var y = Height - 1; y >= 0; y--)
        {
            var full = true;
            for (var x = 0; x < Width; x++) full &= _board[y, x] != 0;
            if (!full) continue;
            for (var copyY = y; copyY > 0; copyY--)
                for (var x = 0; x < Width; x++) _board[copyY, x] = _board[copyY - 1, x];
            for (var x = 0; x < Width; x++) _board[0, x] = 0;
            cleared++;
            y++;
        }
        Lines += cleared;
        Score += cleared switch { 1 => 100, 2 => 300, 3 => 500, 4 => 800, _ => 0 };
    }
}
