namespace Bbs.Tenants.Content.Games;

internal sealed class GameDiagnostics(string game)
{
    private static readonly bool Enabled = ReadEnabled();
    private readonly DateTimeOffset _started = DateTimeOffset.UtcNow;
    private DateTimeOffset _windowStarted = DateTimeOffset.UtcNow;
    private long _frames;
    private long _changedCells;

    public void Event(string message)
    {
        if (Enabled) Log($"event={message}");
    }

    public void Frame(int changedCells)
    {
        if (!Enabled) return;
        _frames++;
        _changedCells += Math.Max(0, changedCells);
        var now = DateTimeOffset.UtcNow;
        var elapsed = now - _windowStarted;
        if (elapsed < TimeSpan.FromSeconds(10)) return;
        Log($"fps={_frames / elapsed.TotalSeconds:F1} changed_cells_per_sec={_changedCells / elapsed.TotalSeconds:F1} uptime_sec={(now - _started).TotalSeconds:F0}");
        _frames = 0; _changedCells = 0; _windowStarted = now;
    }

    private void Log(string message)
        => Console.WriteLine($"[{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}][Game:{game}] {message}");

    private static bool ReadEnabled()
    {
        var raw = Environment.GetEnvironmentVariable("BBS_DEBUG")?.Trim().ToLowerInvariant();
        return raw is "1" or "true" or "yes" or "on";
    }
}
