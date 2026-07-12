namespace Bbs.Tenants.Content.Wopr;

internal enum WoprScene
{
    Logon,
    Conversation,
    ModeSelection,
    SideSelection,
    TargetSelection,
    ConfirmTargets,
    WarSimulation,
    Learning,
    Complete
}

internal sealed class WoprSessionState
{
    public WoprScene Scene { get; set; } = WoprScene.Logon;
    public int ConversationStep { get; set; }
    public bool WarModeSelected { get; set; }
    public string Side { get; set; } = string.Empty;
    public List<string> Targets { get; } = new();
    public int Defcon { get; set; } = 5;
}

internal static class WoprInput
{
    public static string Normalize(string? value)
        => string.Join(' ', (value ?? string.Empty).Trim().ToUpperInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    public static bool IsQuit(string? value) => Normalize(value) is "." or "QUIT";

    public static string? ParseSide(string? value) => Normalize(value) switch
    {
        "1" or "US" or "USA" or "UNITED STATES" => "UNITED STATES",
        "2" or "USSR" or "SOVIET UNION" or "RUSSIA" => "SOVIET UNION",
        _ => null
    };
}

internal sealed class TicTacToeBoard
{
    private readonly char[] _cells = new char[9];

    public char this[int index] => index is >= 0 and < 9 ? _cells[index] : throw new ArgumentOutOfRangeException(nameof(index));

    public bool TryMove(int index, char mark)
    {
        if (index is < 0 or >= 9 || _cells[index] != '\0' || mark is not ('X' or 'O'))
        {
            return false;
        }

        _cells[index] = mark;
        return true;
    }

    public char Winner()
    {
        int[] lines = [0, 1, 2, 3, 4, 5, 6, 7, 8, 0, 3, 6, 1, 4, 7, 2, 5, 8, 0, 4, 8, 2, 4, 6];
        for (var i = 0; i < lines.Length; i += 3)
        {
            var mark = _cells[lines[i]];
            if (mark != '\0' && mark == _cells[lines[i + 1]] && mark == _cells[lines[i + 2]])
            {
                return mark;
            }
        }

        return '\0';
    }

    public bool IsFull => _cells.All(c => c != '\0');

    public int BestMove(char mark)
    {
        if (mark is not ('X' or 'O'))
        {
            throw new ArgumentOutOfRangeException(nameof(mark));
        }

        var bestScore = int.MinValue;
        var bestMove = -1;
        for (var i = 0; i < _cells.Length; i++)
        {
            if (_cells[i] != '\0') continue;
            _cells[i] = mark;
            var score = Minimax(mark, Opponent(mark), false, 0);
            _cells[i] = '\0';
            if (score > bestScore)
            {
                bestScore = score;
                bestMove = i;
            }
        }

        return bestMove;
    }

    private int Minimax(char maximizingMark, char currentMark, bool maximize, int depth)
    {
        var winner = Winner();
        if (winner == maximizingMark) return 10 - depth;
        if (winner == Opponent(maximizingMark)) return depth - 10;
        if (IsFull) return 0;

        var best = maximize ? int.MinValue : int.MaxValue;
        for (var i = 0; i < _cells.Length; i++)
        {
            if (_cells[i] != '\0') continue;
            _cells[i] = currentMark;
            var score = Minimax(maximizingMark, Opponent(currentMark), !maximize, depth + 1);
            _cells[i] = '\0';
            best = maximize ? Math.Max(best, score) : Math.Min(best, score);
        }

        return best;
    }

    private static char Opponent(char mark) => mark == 'X' ? 'O' : 'X';
}
