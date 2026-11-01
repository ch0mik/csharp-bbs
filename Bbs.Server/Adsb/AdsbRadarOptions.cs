using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Bbs.Server.Adsb;

public sealed class AdsbRadarOptions
{
    public const string SectionName = "AdsbRadar";

    public bool Enabled { get; set; }

    [Required]
    public string Provider { get; set; } = "readsb";

    public string FeedUrl { get; set; } = string.Empty;

    [Range(-90, 90)]
    public double? CenterLatitude { get; set; }

    [Range(-180, 180)]
    public double? CenterLongitude { get; set; }

    [Range(1, 250)]
    public double DefaultRangeKm { get; set; } = 25;

    public double[] RangePresetsKm { get; set; } = [10, 25, 50, 100];

    [Range(1, 300)]
    public int PollIntervalSeconds { get; set; } = 15;

    [Range(1, 60)]
    public int RequestTimeoutSeconds { get; set; } = 4;

    [Range(1, 3600)]
    public int StaleAfterSeconds { get; set; } = 30;

    [Range(1, 3600)]
    public int ExpireAfterSeconds { get; set; } = 120;

    [Range(1, 100)]
    public int MaxDisplayedAircraft { get; set; } = 12;

    [Range(1, 1000)]
    public int MaxConcurrentSessions { get; set; } = 100;

    public bool ShowGroundAircraft { get; set; }
    public bool ShowVectors { get; set; } = true;
}

public sealed class AdsbRadarOptionsValidator : IValidateOptions<AdsbRadarOptions>
{
    public ValidateOptionsResult Validate(string? name, AdsbRadarOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var errors = new List<string>();
        if (!string.Equals(options.Provider, "readsb", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(options.Provider, "tar1090", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(options.Provider, "virtualradar", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(options.Provider, "adsbfi", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("AdsbRadar:Provider must be readsb, tar1090, virtualradar, or adsbfi.");
        }

        if (!Uri.TryCreate(options.FeedUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add("AdsbRadar:FeedUrl must be an absolute HTTP or HTTPS URL when the radar is enabled.");
        }

        if (options.CenterLatitude is null || options.CenterLongitude is null)
            errors.Add("AdsbRadar:CenterLatitude and CenterLongitude are required when the radar is enabled.");

        if (options.RequestTimeoutSeconds >= options.PollIntervalSeconds)
            errors.Add("AdsbRadar:RequestTimeoutSeconds must be shorter than PollIntervalSeconds.");
        if (options.StaleAfterSeconds >= options.ExpireAfterSeconds)
            errors.Add("AdsbRadar:StaleAfterSeconds must be shorter than ExpireAfterSeconds.");
        var ranges = options.RangePresetsKm ?? [];
        if (ranges.Length == 0 || ranges.Any(value => value <= 0 || value > 250))
            errors.Add("AdsbRadar:RangePresetsKm must contain values from 1 to 250 km.");
        if (!ranges.Contains(options.DefaultRangeKm))
            errors.Add("AdsbRadar:DefaultRangeKm must be one of RangePresetsKm.");

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
