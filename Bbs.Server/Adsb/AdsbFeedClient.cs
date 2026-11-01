using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Bbs.Server.Adsb;

public interface IAdsbFeedClient
{
    Task<IReadOnlyList<Aircraft>> GetAircraftAsync(CancellationToken cancellationToken);
}

public sealed class AdsbRateLimitException(TimeSpan? retryAfter) : Exception("ADS-B feed rate limit reached.")
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}

public sealed class AdsbFeedClient(IHttpClientFactory clients, IOptions<AdsbRadarOptions> options) : IAdsbFeedClient
{
    private const int MaxResponseBytes = 5 * 1024 * 1024;

    public async Task<IReadOnlyList<Aircraft>> GetAircraftAsync(CancellationToken cancellationToken)
    {
        using var client = clients.CreateClient("adsb");
        using var response = await client.GetAsync(
            options.Value.FeedUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            var retryAfter = response.Headers.RetryAfter?.Delta;
            if (retryAfter is null && response.Headers.RetryAfter?.Date is { } date)
                retryAfter = date - DateTimeOffset.UtcNow;
            throw new AdsbRateLimitException(retryAfter is { } value && value > TimeSpan.Zero ? value : null);
        }
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaxResponseBytes)
            throw new InvalidDataException("ADS-B feed response exceeds the 5 MiB limit.");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload = await ReadBodyAsync(stream, cancellationToken).ConfigureAwait(false);
        await using var payloadStream = new MemoryStream(payload, writable: false);
        using var document = await JsonDocument.ParseAsync(payloadStream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return options.Value.Provider.Equals("virtualradar", StringComparison.OrdinalIgnoreCase)
            ? ParseVirtualRadar(document.RootElement)
            : ParseReadsb(document.RootElement);
    }

    private static async Task<byte[]> ReadBodyAsync(Stream stream, CancellationToken cancellationToken)
    {
        await using var output = new MemoryStream();
        var buffer = new byte[16 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) return output.ToArray();
            if (output.Length + read > MaxResponseBytes)
                throw new InvalidDataException("ADS-B feed response exceeds the 5 MiB limit.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }

    internal static IReadOnlyList<Aircraft> ParseReadsb(JsonElement root)
    {
        // readsb/tar1090 use "aircraft"; adsb.fi's compatible v3 response uses "ac".
        if ((!root.TryGetProperty("aircraft", out var aircraft)
                && !root.TryGetProperty("ac", out aircraft))
            || aircraft.ValueKind != JsonValueKind.Array)
            return [];
        var now = DateTimeOffset.UtcNow;
        return aircraft.EnumerateArray()
            .Select(item => ParseReadsbAircraft(item, now))
            .Where(item => item is not null)
            .Cast<Aircraft>()
            .ToList();
    }

    private static Aircraft? ParseReadsbAircraft(JsonElement item, DateTimeOffset now)
    {
        var latitude = Number(item, "lat");
        var longitude = Number(item, "lon");
        var id = Text(item, "hex");
        if (latitude is null || longitude is null || string.IsNullOrWhiteSpace(id)) return null;
        var altitude = Number(item, "alt_baro");
        var seenPosition = Number(item, "seen_pos") ?? 0;
        return new Aircraft(id, Text(item, "flight")?.Trim(), latitude.Value, longitude.Value, altitude,
            Number(item, "gs"), Number(item, "track"),
            string.Equals(Text(item, "alt_baro"), "ground", StringComparison.OrdinalIgnoreCase),
            now - TimeSpan.FromSeconds(Math.Max(0, seenPosition)));
    }

    internal static IReadOnlyList<Aircraft> ParseVirtualRadar(JsonElement root)
    {
        if (!root.TryGetProperty("acList", out var aircraft) || aircraft.ValueKind != JsonValueKind.Array)
            return [];
        var now = DateTimeOffset.UtcNow;
        return aircraft.EnumerateArray()
            .Select(item =>
            {
                var latitude = Number(item, "Lat"); var longitude = Number(item, "Long");
                var id = Text(item, "Icao");
                if (latitude is null || longitude is null || string.IsNullOrWhiteSpace(id)) return null;
                return new Aircraft(id, Text(item, "Call")?.Trim(), latitude.Value, longitude.Value,
                    Number(item, "Alt"), Number(item, "Spd"), Number(item, "Trak"),
                    item.TryGetProperty("Gnd", out var ground) && ground.ValueKind == JsonValueKind.True, now);
            })
            .Where(item => item is not null).Cast<Aircraft>().ToList();
    }

    private static string? Text(JsonElement item, string property)
        => item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static double? Number(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)) return number;
        return value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number) ? number : null;
    }
}
