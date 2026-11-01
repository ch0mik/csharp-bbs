using System.Threading;

namespace Bbs.Server.Adsb;

public sealed class AdsbRadarSessionLimiter
{
    private int _activeSessions;

    public bool TryEnter(int maximumSessions)
    {
        while (true)
        {
            var current = Volatile.Read(ref _activeSessions);
            if (current >= maximumSessions) return false;
            if (Interlocked.CompareExchange(ref _activeSessions, current + 1, current) == current) return true;
        }
    }

    public void Exit() => Interlocked.Decrement(ref _activeSessions);
}
