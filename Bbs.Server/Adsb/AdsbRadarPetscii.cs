using Bbs.Terminals;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Bbs.Server.Adsb;

public sealed class AdsbRadarPetscii(
    IOptions<AdsbRadarOptions> options,
    IAircraftSnapshotStore snapshots,
    AdsbRadarSessionLimiter sessionLimiter,
    ICitySearchService? citySearch = null) : PetsciiThread
{
    private readonly AdsbRadarOptions _options = options.Value;
    private int _rangeIndex;
    private int _selected;
    private Cell[,]? _previousGrid;
    private string[]? _previousStatus;
    private ClientLocation? _center;

    public override async Task DoLoopAsync(CancellationToken cancellationToken = default)
    {
        if (!sessionLimiter.TryEnter(_options.MaxConcurrentSessions))
        {
            Cls();
            Println("ADS-B RADAR BUSY");
            Println("RADAR SESSION LIMIT REACHED.");
            Println("Press ENTER to return.");
            await FlushAsync(cancellationToken).ConfigureAwait(false);
            await ReadLineAsync(maxLength: 1, cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            _rangeIndex = Array.FindIndex(_options.RangePresetsKm, value => value == _options.DefaultRangeKm);
            if (_rangeIndex < 0) _rangeIndex = 0;
            _center = GetSessionCenter();
            if (!await ConfigureCenterAsync(cancellationToken).ConfigureAwait(false)) return;

            while (!cancellationToken.IsCancellationRequested)
            {
                Render();
                await FlushAsync(cancellationToken).ConfigureAwait(false);
                var key = await KeyPressedAsync(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                switch (key)
                {
                    case 'q': case 'Q': case '.': case 3: return;
                    case '+': case '=': _rangeIndex = (_rangeIndex + 1) % _options.RangePresetsKm.Length; break;
                    case '-': _rangeIndex = (_rangeIndex + _options.RangePresetsKm.Length - 1) % _options.RangePresetsKm.Length; break;
                    case 'n': case 'N': _selected++; break;
                    case 'p': case 'P': _selected--; break;
                }
            }
        }
        finally
        {
            sessionLimiter.Exit();
        }
    }

    private void Render()
    {
        var snapshot = snapshots.Current;
        var snapshotAge = snapshot.RetrievedAt is null ? (TimeSpan?)null : DateTimeOffset.UtcNow - snapshot.RetrievedAt.Value;
        var isExpired = snapshotAge is { } snapshotAgeValue && snapshotAgeValue > TimeSpan.FromSeconds(_options.ExpireAfterSeconds);
        var isStale = snapshotAge is { } staleAge && staleAge > TimeSpan.FromSeconds(_options.StaleAfterSeconds);
        if (isExpired)
        {
            snapshot = snapshot with { Aircraft = [] };
        }
        var rangeKm = _options.RangePresetsKm[_rangeIndex];
        var aircraft = snapshot.Aircraft
            .Where(plane => _options.ShowGroundAircraft || !plane.IsOnGround)
            .Select(plane => new RadarTarget(plane, DistanceKm(plane), Bearing(plane)))
            .OrderBy(target => target.DistanceKm)
            .Take(_options.MaxDisplayedAircraft)
            .ToList();
        if (aircraft.Count > 0) _selected = ((_selected % aircraft.Count) + aircraft.Count) % aircraft.Count;
        else _selected = 0;

        var age = snapshot.RetrievedAt is null ? "NO FEED" : isExpired ? "EXPIRED" : isStale ? $"STALE {snapshotAge!.Value.TotalSeconds:0}s" : $"{snapshotAge!.Value.TotalSeconds:0}s";
        var center = GetCenter();
        var status = new string[4];
        status[0] = $"RNG {rangeKm:0}KM  {aircraft.Count,2} A/C  AGE {age}".PadRight(39);
        if (aircraft.Count > 0)
        {
            var target = aircraft[_selected];
            var label = string.IsNullOrWhiteSpace(target.Aircraft.Callsign) ? target.Aircraft.Id : target.Aircraft.Callsign!;
            status[1] = $"{label[..Math.Min(8, label.Length)]} {target.DistanceKm,4:0.0}KM {target.Aircraft.AltitudeFeet ?? 0,5:0}FT".PadRight(39);
        }
        else status[1] = (snapshot.LastError is null ? "WAITING FOR AIRCRAFT DATA" : "FEED ERROR - USING LAST DATA").PadRight(39);
        status[2] = $"{center.Source} {center.Latitude:0.0000},{center.Longitude:0.0000}".PadRight(39);
        status[3] = "+/- RANGE N/P SELECT Q BACK".PadRight(39);

        RenderChanges(BuildGrid(rangeKm, aircraft), status);
    }

    private void RenderChanges(Cell[,] grid, string[] status)
    {
        if (_previousGrid is null)
        {
            Cls();
            Write(PetsciiKeys.Cyan); Println("ADS-B SONAR RADAR".PadRight(39));
            for (var row = 0; row < grid.GetLength(0); row++)
            {
                for (var column = 0; column < grid.GetLength(1); column++)
                {
                    var cell = grid[row, column];
                    Write(cell.Color, cell.Character);
                }
                Println();
            }
            for (var index = 0; index < status.Length; index++) DrawStatus(index, status[index]);
        }
        else
        {
            for (var row = 0; row < grid.GetLength(0); row++)
                for (var column = 0; column < grid.GetLength(1); column++)
                {
                    if (grid[row, column] == _previousGrid[row, column]) continue;
                    PositionCursor(column, row + 1);
                    Write(grid[row, column].Color, grid[row, column].Character);
                }
            for (var index = 0; index < status.Length; index++)
                if (_previousStatus![index] != status[index]) DrawStatus(index, status[index]);
        }

        _previousGrid = grid;
        _previousStatus = status;
        PositionCursor(0, 24);
    }

    private void DrawStatus(int row, string text)
    {
        PositionCursor(0, row + 20);
        Write(row >= 2 ? PetsciiKeys.LightGray : PetsciiKeys.White);
        Print(text);
    }

    private void PositionCursor(int x, int y)
    {
        Write(PetsciiKeys.Home);
        for (var row = 0; row < y; row++) Write(PetsciiKeys.Down);
        for (var column = 0; column < x; column++) Write(PetsciiKeys.Right);
    }

    private Cell[,] BuildGrid(double rangeKm, IReadOnlyList<RadarTarget> aircraft)
    {
        const int width = 39, height = 19, centerX = 19, centerY = 9;
        var grid = new Cell[height, width];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var dx = (x - centerX) / 2.0; var dy = y - centerY;
                var radius = Math.Sqrt(dx * dx + dy * dy);
                var ring = Math.Min(width / 4.0, height / 2.0);
                var isRing = Enumerable.Range(1, 4).Any(index => Math.Abs(radius - ring * index / 4) < .45);
                grid[y, x] = new Cell(isRing ? '.' : ' ', isRing ? PetsciiKeys.Green : PetsciiKeys.Blue);
            }
        for (var y = 0; y < height; y++) grid[y, centerX] = new Cell('|', PetsciiKeys.Green);
        for (var x = 0; x < width; x++) grid[centerY, x] = new Cell('-', PetsciiKeys.Green);
        grid[0, centerX] = new Cell('N', PetsciiKeys.White);
        grid[height - 1, centerX] = new Cell('S', PetsciiKeys.White);
        grid[centerY, 0] = new Cell('W', PetsciiKeys.White);
        grid[centerY, width - 1] = new Cell('E', PetsciiKeys.White);
        grid[centerY, centerX] = new Cell('*', PetsciiKeys.White);

        for (var index = 0; index < aircraft.Count; index++)
        {
            var target = aircraft[index];
            var ratio = Math.Min(1, target.DistanceKm / rangeKm);
            var radians = target.BearingDegrees * Math.PI / 180;
            var x = (int)Math.Round(centerX + Math.Sin(radians) * ratio * (width / 2.0 - 2));
            var y = (int)Math.Round(centerY - Math.Cos(radians) * ratio * (height / 2.0 - 1));
            x = Math.Clamp(x, 1, width - 2); y = Math.Clamp(y, 1, height - 2);
            if (_options.ShowVectors && target.Aircraft.TrackDegrees is { } track)
            {
                var trackRadians = track * Math.PI / 180;
                var vectorX = Math.Clamp(x + (int)Math.Round(Math.Sin(trackRadians) * 2), 1, width - 2);
                var vectorY = Math.Clamp(y - (int)Math.Round(Math.Cos(trackRadians)), 1, height - 2);
                if (vectorX != x || vectorY != y)
                    grid[vectorY, vectorX] = new Cell('.', PetsciiKeys.Purple);
            }
            grid[y, x] = new Cell(HeadingGlyph(target.Aircraft.TrackDegrees), index == _selected ? PetsciiKeys.Yellow : PetsciiKeys.Red);
        }
        return grid;
    }

    private double DistanceKm(Aircraft aircraft)
    {
        var center = GetCenter();
        const double earthRadius = 6371;
        var lat1 = center.Latitude * Math.PI / 180; var lat2 = aircraft.Latitude * Math.PI / 180;
        var dLat = lat2 - lat1; var dLon = (aircraft.Longitude - center.Longitude) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return earthRadius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private double Bearing(Aircraft aircraft)
    {
        var center = GetCenter();
        var lat1 = center.Latitude * Math.PI / 180; var lat2 = aircraft.Latitude * Math.PI / 180;
        var dLon = (aircraft.Longitude - center.Longitude) * Math.PI / 180;
        var angle = Math.Atan2(Math.Sin(dLon) * Math.Cos(lat2), Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon));
        return (angle * 180 / Math.PI + 360) % 360;
    }

    private static char HeadingGlyph(double? heading) => heading switch
    {
        null => '*',
        < 45 or >= 315 => '^',
        < 135 => '>',
        < 225 => 'v',
        _ => '<'
    };

    private ClientLocation GetCenter()
        => _center ?? GetSessionCenter();

    private ClientLocation GetSessionCenter()
        => GetCustomObject(ClientLocation.SessionKey) as ClientLocation
            ?? new ClientLocation(_options.CenterLatitude!.Value, _options.CenterLongitude!.Value, "DEFAULT");

    private async Task<bool> ConfigureCenterAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Cls();
            Write(PetsciiKeys.Cyan); Println("ADS-B RADAR CENTER"); Write(PetsciiKeys.White);
            Println();
            Println($"SOURCE: {_center!.Source}");
            Println($"LAT:    {_center.Latitude:0.000000}");
            Println($"LON:    {_center.Longitude:0.000000}");
            Println();
            Println("ENTER=start  M=modify  S=search");
            Print("Q=back: ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);
            var choice = (await ReadLineAsync(maxLength: 1, cancellationToken: cancellationToken).ConfigureAwait(false)).Trim();
            if (choice.Equals("Q", StringComparison.OrdinalIgnoreCase) || choice == ".")
                return false;
            if (choice.Length == 0) return true;
            if (choice.Equals("S", StringComparison.OrdinalIgnoreCase))
            {
                await SearchCenterAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }
            if (!choice.Equals("M", StringComparison.OrdinalIgnoreCase)) continue;

            Print("Latitude (-90..90): ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);
            var latitudeText = await ReadLineAsync(maxLength: 16, cancellationToken: cancellationToken).ConfigureAwait(false);
            Print("Longitude (-180..180): ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);
            var longitudeText = await ReadLineAsync(maxLength: 16, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (double.TryParse(latitudeText, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude)
                && double.TryParse(longitudeText, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude)
                && latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180)
            {
                _center = new ClientLocation(latitude, longitude, "MANUAL");
                SetCustomObject(ClientLocation.SessionKey, _center);
            }
            else
            {
                Println();
                Println("INVALID COORDINATES - CENTER UNCHANGED.");
                Print("Press ENTER...");
                await FlushAsync(cancellationToken).ConfigureAwait(false);
                await ReadLineAsync(maxLength: 1, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
        return false;
    }

    private async Task SearchCenterAsync(CancellationToken cancellationToken)
    {
        Cls();
        Write(PetsciiKeys.Cyan); Println("SEARCH RADAR CENTER"); Write(PetsciiKeys.White);
        Println("City name (2-64 chars), ENTER=back:");
        await FlushAsync(cancellationToken).ConfigureAwait(false);
        var query = (await ReadLineAsync(maxLength: 64, cancellationToken: cancellationToken).ConfigureAwait(false)).Trim();
        if (query.Length == 0) return;
        if (query.Length < 2 || query.Any(char.IsControl))
        {
            await ShowSearchMessageAsync("INVALID CITY NAME.", cancellationToken).ConfigureAwait(false);
            return;
        }
        Println("SEARCHING...");
        await FlushAsync(cancellationToken).ConfigureAwait(false);
        var matches = citySearch is null ? null : await citySearch.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        if (matches is null || matches.Count == 0)
        {
            await ShowSearchMessageAsync(matches is null ? "SEARCH UNAVAILABLE. TRY AGAIN LATER." : "NO MATCHING CITIES.", cancellationToken).ConfigureAwait(false);
            return;
        }
        Cls();
        Write(PetsciiKeys.Cyan); Println("SELECT CITY"); Write(PetsciiKeys.White);
        for (var index = 0; index < matches.Count; index++)
        {
            var match = matches[index];
            Println($"{index + 1}) {match.Name}");
            Println($"   Country: {DisplayArea(match.Country)}");
            Println($"   State:   {DisplayArea(match.Region)}");
            Println($"   County:  {DisplayArea(match.County)}");
        }
        Println("GeoNames via Open-Meteo.com");
        Print("Number (ENTER=back): ");
        await FlushAsync(cancellationToken).ConfigureAwait(false);
        var input = await ReadLineAsync(maxLength: 2, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(input)) return;
        if (int.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out var selected) && selected >= 1 && selected <= matches.Count)
        {
            var match = matches[selected - 1];
            _center = new ClientLocation(match.Latitude, match.Longitude, "SEARCH");
            SetCustomObject(ClientLocation.SessionKey, _center);
        }
        else await ShowSearchMessageAsync("INVALID SELECTION.", cancellationToken).ConfigureAwait(false);
    }

    private async Task ShowSearchMessageAsync(string message, CancellationToken cancellationToken)
    {
        Println(message);
        Print("Press ENTER...");
        await FlushAsync(cancellationToken).ConfigureAwait(false);
        await ReadLineAsync(maxLength: 1, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static string DisplayArea(string value) => string.IsNullOrWhiteSpace(value) ? "-" : value;

    private readonly record struct RadarTarget(Aircraft Aircraft, double DistanceKm, double BearingDegrees);
    private readonly record struct Cell(char Character, int Color);
}
