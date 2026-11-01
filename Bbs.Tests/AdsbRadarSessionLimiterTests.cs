using Bbs.Server.Adsb;

namespace Bbs.Tests;

public sealed class AdsbRadarSessionLimiterTests
{
    [Fact]
    public void Allows_at_most_the_configured_number_of_radar_sessions()
    {
        var limiter = new AdsbRadarSessionLimiter();

        Assert.True(limiter.TryEnter(2));
        Assert.True(limiter.TryEnter(2));
        Assert.False(limiter.TryEnter(2));

        limiter.Exit();

        Assert.True(limiter.TryEnter(2));
    }
}
