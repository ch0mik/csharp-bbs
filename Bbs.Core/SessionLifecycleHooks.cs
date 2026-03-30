namespace Bbs.Core;

public static class SessionLifecycleHooks
{
    public static Action<BbsThread>? OnSessionStarted { get; set; }

    public static Action<BbsThread>? OnSessionEnded { get; set; }

    internal static void RaiseSessionStarted(BbsThread thread)
    {
        OnSessionStarted?.Invoke(thread);
    }

    internal static void RaiseSessionEnded(BbsThread thread)
    {
        OnSessionEnded?.Invoke(thread);
    }
}
