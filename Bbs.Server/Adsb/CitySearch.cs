using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bbs.Server.Adsb;

public sealed class CitySearchOptions
{
    public const string SectionName = "CitySearch";
    public string Endpoint { get; set; } = "https://geocoding-api.open-meteo.com/v1/search";
    public int RequestTimeoutSeconds { get; set; } = 5;
}

public sealed class CitySearchOptionsValidator : IValidateOptions<CitySearchOptions>
{
    public ValidateOptionsResult Validate(string? name, CitySearchOptions options)
        => Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo)
            && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
            && options.RequestTimeoutSeconds is >= 1 and <= 10
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail("CitySearch requires an absolute HTTPS Endpoint without credentials, query or fragment, and RequestTimeoutSeconds between 1 and 10.");
}

public sealed record CitySearchResult(string Name, string Country, string Region, string County, double Latitude, double Longitude);

public interface ICitySearchService
{
    // Null means provider unavailable; an empty list means no matching city.
    Task<IReadOnlyList<CitySearchResult>?> SearchAsync(string query, CancellationToken cancellationToken);
}

public sealed class CitySearchService(IHttpClientFactory clients, IOptions<CitySearchOptions> options,
    ILogger<CitySearchService> logger) : ICitySearchService
{
    public async Task<IReadOnlyList<CitySearchResult>?> SearchAsync(string query, CancellationToken cancellationToken)
    {
        query = query.Trim();
        if (query.Length is < 2 or > 64 || query.Any(char.IsControl)) return [];
        var url = $"{options.Value.Endpoint}?name={Uri.EscapeDataString(query)}&count=5&language=en&format=json";
        try
        {
            // A linked deadline covers both response headers and the bounded body read.
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(options.Value.RequestTimeoutSeconds));
            using var client = clients.CreateClient("city-search");
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;
            await response.Content.LoadIntoBufferAsync(64 * 1024, deadline.Token).ConfigureAwait(false);
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions { MaxDepth = 16 }, deadline.Token).ConfigureAwait(false);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("results", out var results)) return [];
            if (results.ValueKind != JsonValueKind.Array) return null;
            var matches = new List<CitySearchResult>(5);
            foreach (var item in results.EnumerateArray().Take(5))
            {
                if (item.ValueKind != JsonValueKind.Object
                    || !Number(item, "latitude", out var latitude) || !Number(item, "longitude", out var longitude)
                    || !double.IsFinite(latitude) || !double.IsFinite(longitude)
                    || latitude is < -90 or > 90 || longitude is < -180 or > 180) continue;
                var city = SafeText(item, "name", 24);
                if (city.Length == 0) continue;
                var country = SafeText(item, "country", 27);
                if (country.Length == 0) country = SafeText(item, "country_code", 2);
                matches.Add(new(city, country, SafeText(item, "admin1", 27), SafeText(item, "admin2", 27), latitude, longitude));
            }
            return matches;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException)
        {
            logger.LogDebug(exception, "City search provider unavailable");
            return null;
        }
    }

    private static bool Number(JsonElement item, string name, out double value)
    {
        value = default;
        return item.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out value);
    }

    private static string SafeText(JsonElement item, string name, int maxLength)
    {
        if (!item.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String) return "";
        var normalized = (element.GetString() ?? "").Replace('ł', 'l').Replace('Ł', 'L').Normalize(NormalizationForm.FormD);
        // PETSCII receives printable ASCII only: no remote terminal control characters.
        return new string(normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark
            && c is >= ' ' and <= '~').Take(maxLength).ToArray()).Trim();
    }
}
