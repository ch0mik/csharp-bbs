using System.Text.Json;
using Bbs.Server.Adsb;

namespace Bbs.Tests;

public sealed class AdsbFeedParserTests
{
    [Fact]
    public void Readsb_parser_keeps_position_and_marks_ground_aircraft()
    {
        using var document = JsonDocument.Parse("""
            { "aircraft": [
              { "hex": "abc123", "flight": "LOT123 ", "lat": 52.20, "lon": 21.00,
                "alt_baro": "ground", "gs": 15.5, "track": 180, "seen_pos": 2 }
            ] }
            """);

        var aircraft = AdsbFeedClient.ParseReadsb(document.RootElement);

        var item = Assert.Single(aircraft);
        Assert.Equal("abc123", item.Id);
        Assert.Equal("LOT123", item.Callsign);
        Assert.True(item.IsOnGround);
        Assert.Null(item.AltitudeFeet);
        Assert.Equal(180, item.TrackDegrees);
    }

    [Fact]
    public void Readsb_parser_ignores_aircraft_without_position()
    {
        using var document = JsonDocument.Parse("""{ "aircraft": [{ "hex": "abc123", "flight": "LOT123" }] }""");

        Assert.Empty(AdsbFeedClient.ParseReadsb(document.RootElement));
    }

    [Fact]
    public void Virtual_radar_parser_maps_aircraft_list()
    {
        using var document = JsonDocument.Parse("""
            { "acList": [
              { "Icao": "4ca001", "Call": "RYR42 ", "Lat": 52.10, "Long": 21.10,
                "Alt": 12000, "Spd": 420, "Trak": 90, "Gnd": false }
            ] }
            """);

        var item = Assert.Single(AdsbFeedClient.ParseVirtualRadar(document.RootElement));

        Assert.Equal("4ca001", item.Id);
        Assert.Equal("RYR42", item.Callsign);
        Assert.Equal(12000, item.AltitudeFeet);
        Assert.Equal(420, item.GroundSpeedKnots);
        Assert.False(item.IsOnGround);
    }
}
