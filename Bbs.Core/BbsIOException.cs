namespace Bbs.Core;

public sealed class BbsIOException : IOException
{
    public BbsIOException(string message) : base(message)
    {
    }
}
