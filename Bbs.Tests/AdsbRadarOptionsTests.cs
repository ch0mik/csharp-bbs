using Bbs.Server.Adsb;
using Microsoft.Extensions.Options;

namespace Bbs.Tests;

public sealed class AdsbRadarOptionsTests
{
    private readonly AdsbRadarOptionsValidator _validator = new();

    [Fact]
    public void Disabled_radar_does_not_require_a_feed()
    {
        var result = _validator.Validate(null, new AdsbRadarOptions { Enabled = false });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Enabled_radar_rejects_invalid_feed_and_timeout()
    {
        var options = new AdsbRadarOptions
        {
            Enabled = true,
            FeedUrl = "ftp://receiver/aircraft.json",
            RequestTimeoutSeconds = 5,
            PollIntervalSeconds = 5
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("FeedUrl", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, failure => failure.Contains("RequestTimeoutSeconds", StringComparison.Ordinal));
    }

    [Fact]
    public void Valid_readsb_configuration_is_accepted()
    {
        var options = new AdsbRadarOptions
        {
            Enabled = true,
            FeedUrl = "http://readsb.local/tar1090/data/aircraft.json",
            CenterLatitude = 52.2297,
            CenterLongitude = 21.0122
        };

        Assert.True(_validator.Validate(null, options).Succeeded);
    }

    [Fact]
    public void Enabled_radar_requires_an_explicit_center()
    {
        var options = new AdsbRadarOptions
        {
            Enabled = true,
            FeedUrl = "http://readsb.local/tar1090/data/aircraft.json",
            CenterLatitude = null,
            CenterLongitude = null
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("CenterLatitude", StringComparison.Ordinal));
    }

    [Fact]
    public void Rate_limit_delay_is_clamped_to_a_bounded_window()
    {
        var delay = AdsbPollingService.CalculateDelay(5, 1, TimeSpan.FromDays(1));

        Assert.Equal(TimeSpan.FromSeconds(120), delay);
    }
}
