using System.Net.Sockets;
using System.Text;
using Bbs.Server.Adsb;
using Microsoft.Extensions.Options;

namespace Bbs.Tests;

public sealed class AdsbCitySearchSessionTests
{
    [Fact]
    public async Task SelectedCityRequiresConfirmationAndIsStoredInSession()
    {
        var pair = await TestSocketPair.CreateAsync();
        using var server = pair.Server;
        using var client = pair.Client;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var token = deadline.Token;
        var radar = new AdsbRadarPetscii(Options.Create(new AdsbRadarOptions { CenterLatitude = 52.2297, CenterLongitude = 21.0122 }),
            new AircraftSnapshotStore(), new AdsbRadarSessionLimiter(), new SearchStub());
        var session = radar.RunSessionAsync(server, TimeSpan.FromMinutes(1), token);
        var stream = client.GetStream();
        var initial = await ReadUntilAsync(stream, "Q=back:", token);
        Assert.Contains("S=search", initial, StringComparison.OrdinalIgnoreCase);
        await stream.WriteAsync(Encoding.ASCII.GetBytes("s\r"), token);
        await ReadUntilAsync(stream, "ENTER=back:", token);
        await stream.WriteAsync(Encoding.ASCII.GetBytes("Lodz\r"), token);
        var choices = await ReadUntilAsync(stream, "Number (ENTER=back):", token);
        Assert.Contains("1) Lodz", choices, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Country: Poland", choices, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("State:   Lodzkie", choices, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("County:  Lodz County", choices, StringComparison.OrdinalIgnoreCase);
        await stream.WriteAsync(Encoding.ASCII.GetBytes("1\r"), token);
        var confirmation = await ReadUntilAsync(stream, "Q=back:", token);
        Assert.Contains("SOURCE: SEARCH", confirmation, StringComparison.OrdinalIgnoreCase);
        // Formatting follows the current culture, so assert the location through session state.
        Assert.DoesNotContain("ADS-B SONAR RADAR", confirmation, StringComparison.OrdinalIgnoreCase);
        var center = Assert.IsType<ClientLocation>(radar.GetCustomObject(ClientLocation.SessionKey));
        Assert.Equal(51.7592, center.Latitude);
        Assert.Equal(19.456, center.Longitude);
        await stream.WriteAsync(Encoding.ASCII.GetBytes("q\r"), token);
        await session.WaitAsync(token);
    }

    private static async Task<string> ReadUntilAsync(NetworkStream stream, string marker, CancellationToken cancellationToken)
    {
        var captured = new StringBuilder();
        var buffer = new byte[4096];
        while (!captured.ToString().Contains(marker, StringComparison.OrdinalIgnoreCase))
        {
            var count = await stream.ReadAsync(buffer, cancellationToken);
            if (count == 0) throw new EndOfStreamException();
            captured.Append(Encoding.Latin1.GetString(buffer, 0, count));
        }
        return captured.ToString();
    }

    private sealed class SearchStub : ICitySearchService
    {
        public Task<IReadOnlyList<CitySearchResult>?> SearchAsync(string query, CancellationToken cancellationToken)
        {
            Assert.Equal("Lodz", query, ignoreCase: true);
            Assert.True(cancellationToken.CanBeCanceled);
            return Task.FromResult<IReadOnlyList<CitySearchResult>?>([new("Lodz", "Poland", "Lodzkie", "Lodz County", 51.7592, 19.456)]);
        }
    }
}
