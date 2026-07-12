namespace Bbs.Tenants.Content.Chess;

internal sealed class ChessBoard
{
    private readonly char[] _squares = new char[64];

    public ChessBoard()
    {
        const string initial = "RNBQKBNRPPPPPPPP                                pppppppprnbqkbnr";
        for (var i = 0; i < 64; i++) _squares[i] = initial[i];
    }

    public char this[int file, int rank] => IsInside(file, rank) ? _squares[Index(file, rank)] : '\0';

    public bool TryMove(string? notation, bool white)
    {
        if (!TryParse(notation, out var fromFile, out var fromRank, out var toFile, out var toRank)) return false;
        if (!IsLegal(fromFile, fromRank, toFile, toRank, white)) return false;
        var piece = this[fromFile, fromRank];
        _squares[Index(toFile, toRank)] = PromoteIfNeeded(piece, toRank);
        _squares[Index(fromFile, fromRank)] = ' ';
        return true;
    }

    public string? BestMove(bool white)
    {
        var moves = LegalMoves(white).ToArray();
        return moves
            .OrderByDescending(m => PieceValue(this[m.ToFile, m.ToRank]))
            .ThenBy(m => m.Notation, StringComparer.Ordinal)
            .Select(m => m.Notation)
            .FirstOrDefault();
    }

    public bool HasKing(bool white) => _squares.Contains(white ? 'K' : 'k');

    private IEnumerable<Move> LegalMoves(bool white)
    {
        for (var fromRank = 0; fromRank < 8; fromRank++)
        for (var fromFile = 0; fromFile < 8; fromFile++)
        for (var toRank = 0; toRank < 8; toRank++)
        for (var toFile = 0; toFile < 8; toFile++)
        {
            if (!IsLegal(fromFile, fromRank, toFile, toRank, white)) continue;
            yield return new Move(fromFile, fromRank, toFile, toRank, ToNotation(fromFile, fromRank, toFile, toRank));
        }
    }

    private bool IsLegal(int fromFile, int fromRank, int toFile, int toRank, bool white)
    {
        if (!IsInside(fromFile, fromRank) || !IsInside(toFile, toRank) || (fromFile == toFile && fromRank == toRank)) return false;
        var piece = this[fromFile, fromRank];
        var target = this[toFile, toRank];
        if (piece == ' ' || char.IsUpper(piece) != white || (target != ' ' && char.IsUpper(target) == white)) return false;
        var dx = toFile - fromFile;
        var dy = toRank - fromRank;
        return char.ToUpperInvariant(piece) switch
        {
            'P' => IsPawnMove(fromFile, fromRank, toFile, toRank, white, target),
            'N' => (Math.Abs(dx), Math.Abs(dy)) is (1, 2) or (2, 1),
            'B' => Math.Abs(dx) == Math.Abs(dy) && PathClear(fromFile, fromRank, toFile, toRank),
            'R' => (dx == 0 || dy == 0) && PathClear(fromFile, fromRank, toFile, toRank),
            'Q' => (dx == 0 || dy == 0 || Math.Abs(dx) == Math.Abs(dy)) && PathClear(fromFile, fromRank, toFile, toRank),
            'K' => Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1,
            _ => false
        };
    }

    private bool IsPawnMove(int fromFile, int fromRank, int toFile, int toRank, bool white, char target)
    {
        var direction = white ? 1 : -1;
        var startRank = white ? 1 : 6;
        var dx = Math.Abs(toFile - fromFile);
        var dy = toRank - fromRank;
        if (dx == 1 && dy == direction) return target != ' ';
        if (dx != 0 || target != ' ') return false;
        if (dy == direction) return true;
        return fromRank == startRank && dy == 2 * direction && this[fromFile, fromRank + direction] == ' ';
    }

    private bool PathClear(int fromFile, int fromRank, int toFile, int toRank)
    {
        var stepFile = Math.Sign(toFile - fromFile);
        var stepRank = Math.Sign(toRank - fromRank);
        var file = fromFile + stepFile;
        var rank = fromRank + stepRank;
        while (file != toFile || rank != toRank)
        {
            if (this[file, rank] != ' ') return false;
            file += stepFile;
            rank += stepRank;
        }
        return true;
    }

    private static bool TryParse(string? value, out int fromFile, out int fromRank, out int toFile, out int toRank)
    {
        var text = (value ?? string.Empty).Trim().ToUpperInvariant().Replace("-", string.Empty, StringComparison.Ordinal);
        fromFile = fromRank = toFile = toRank = -1;
        if (text.Length != 4 || text[0] is < 'A' or > 'H' || text[2] is < 'A' or > 'H'
            || text[1] is < '1' or > '8' || text[3] is < '1' or > '8') return false;
        fromFile = text[0] - 'A';
        fromRank = text[1] - '1';
        toFile = text[2] - 'A';
        toRank = text[3] - '1';
        return true;
    }

    private static char PromoteIfNeeded(char piece, int rank)
        => piece == 'P' && rank == 7 ? 'Q' : piece == 'p' && rank == 0 ? 'q' : piece;
    private static int PieceValue(char piece) => char.ToUpperInvariant(piece) switch { 'Q' => 9, 'R' => 5, 'B' or 'N' => 3, 'P' => 1, _ => 0 };
    private static bool IsInside(int file, int rank) => file is >= 0 and < 8 && rank is >= 0 and < 8;
    private static int Index(int file, int rank) => rank * 8 + file;
    private static string ToNotation(int ff, int fr, int tf, int tr) => $"{(char)('A' + ff)}{fr + 1}{(char)('A' + tf)}{tr + 1}";
    private sealed record Move(int FromFile, int FromRank, int ToFile, int ToRank, string Notation);
}
