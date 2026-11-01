using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bbs.Server.Adsb;

public sealed class ClientGeolocationOptions
{
    public const string SectionName = "ClientGeolocation";

    public bool Enabled { get; set; } = true;
    public string EndpointTemplate { get; set; } = "https://ipwho.is/{ip}";
    public string PrivateAddressFallbackEndpoint { get; set; } = "https://ipwho.is/";
    public int RequestTimeoutSeconds { get; set; } = 2;
}

public sealed class ClientGeolocationOptionsValidator : IValidateOptions<ClientGeolocationOptions>
{
    public ValidateOptionsResult Validate(string? name, ClientGeolocationOptions options)
    {
        if (!options.Enabled) return ValidateOptionsResult.Success;
        if (!options.EndpointTemplate.Contains("{ip}", StringComparison.Ordinal)
            || !Uri.TryCreate(options.EndpointTemplate.Replace("{ip}", "127.0.0.1", StringComparison.Ordinal), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
            return ValidateOptionsResult.Fail("ClientGeolocation:EndpointTemplate must be an absolute HTTPS URL containing {ip}.");
        if (options.RequestTimeoutSeconds is < 1 or > 10)
            return ValidateOptionsResult.Fail("ClientGeolocation:RequestTimeoutSeconds must be between 1 and 10.");
        if (!Uri.TryCreate(options.PrivateAddressFallbackEndpoint, UriKind.Absolute, out var fallbackUri)
            || fallbackUri.Scheme != Uri.UriSchemeHttps)
            return ValidateOptionsResult.Fail("ClientGeolocation:PrivateAddressFallbackEndpoint must be an absolute HTTPS URL.");
        return ValidateOptionsResult.Success;
    }
}

public sealed record ClientLocation(double Latitude, double Longitude, string Source)
{
    public const string SessionKey = "session:client-location";
}

public interface IClientGeolocationService
{
    Task<ClientLocation?> LocateAsync(IPAddress address, CancellationToken cancellationToken);
}

public sealed class ClientGeolocationService(IHttpClientFactory clients, IOptions<ClientGeolocationOptions> options, ILogger<ClientGeolocationService> logger) : IClientGeolocationService
{
    public async Task<ClientLocation?> LocateAsync(IPAddress address, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled) return null;

        var privateAddress = IsPrivate(address);
        var url = privateAddress
            ? settings.PrivateAddressFallbackEndpoint
            : settings.EndpointTemplate.Replace("{ip}", Uri.EscapeDataString(address.ToString()), StringComparison.Ordinal);
        try
        {
            using var client = clients.CreateClient("client-geolocation");
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = document.RootElement;
            if (root.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.False) return null;
            if (!TryNumber(root, "latitude", out var latitude) || !TryNumber(root, "longitude", out var longitude)) return null;
            if (latitude is < -90 or > 90 || longitude is < -180 or > 180) return null;
            return new ClientLocation(latitude, longitude, privateAddress ? "HOST GEO" : "IP GEO");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Client geolocation failed for {Address}", address);
            return null;
        }
    }

    private static bool IsPrivate(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal) return true;
        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
        var octets = address.GetAddressBytes();
        return octets[0] == 10
            || octets[0] == 127
            || (octets[0] == 192 && octets[1] == 168)
            || (octets[0] == 172 && octets[1] is >= 16 and <= 31);
    }

    private static bool TryNumber(JsonElement root, string property, out double value)
    {
        value = default;
        return root.TryGetProperty(property, out var element)
            && element.ValueKind == JsonValueKind.Number
            && element.TryGetDouble(out value);
    }
}
