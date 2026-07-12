namespace Bbs.Tenants.Content.Thermonuclear;

internal enum NuclearSide { UnitedStates, SovietUnion }
internal enum StrikeResult { Miss, Marginal, Minor, Major, Critical }
internal enum PeaceResult { Progress, SurpriseAttack, NoProgress }

internal sealed class NuclearCity
{
    public required string Name { get; init; }
    public required NuclearSide Owner { get; init; }
    public required int InitialPopulation { get; init; }
    public int Population { get; set; }
    public char LastImpact { get; set; } = ' ';
}

internal sealed record StrikeReport(string Target, StrikeResult Result, int Casualties, int RemainingPopulation, char Marker);

internal sealed class ThermonuclearGame
{
    private readonly Random _random;

    public ThermonuclearGame(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : Random.Shared;
        Cities = CreateCities();
    }

    public List<NuclearCity> Cities { get; }
    public int UnitedStatesMissiles { get; private set; } = 32;
    public int SovietUnionMissiles { get; private set; } = 32;
    public int PeaceProgress { get; private set; }
    public NuclearSide? Winner { get; private set; }
    public bool IsDraw { get; private set; }
    public bool IsOver => Winner.HasValue || IsDraw || PeaceProgress >= 5 || Population(NuclearSide.UnitedStates) == 0 || Population(NuclearSide.SovietUnion) == 0;

    public int Missiles(NuclearSide side) => side == NuclearSide.UnitedStates ? UnitedStatesMissiles : SovietUnionMissiles;
    public int MaxTargets(NuclearSide side) => side == NuclearSide.UnitedStates ? 4 : 6;
    public int Population(NuclearSide side) => Cities.Where(c => c.Owner == side).Sum(c => c.Population);

    public IReadOnlyList<StrikeReport> Launch(NuclearSide attacker, IEnumerable<string> targetNames)
    {
        var available = Missiles(attacker);
        var targets = targetNames
            .Select(name => Cities.FirstOrDefault(c => string.Equals(c.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)))
            .Where(c => c is not null && c.Owner != attacker && c.Population > 0)
            .Cast<NuclearCity>()
            .Distinct()
            .Take(Math.Min(MaxTargets(attacker), available))
            .ToArray();
        var reports = targets.Select(Fire).ToArray();
        ChangeMissiles(attacker, -reports.Length);
        EvaluateOutcome();
        return reports;
    }

    public PeaceResult Negotiate(NuclearSide initiator)
    {
        var roll = _random.Next(100);
        if (roll < 20)
        {
            PeaceProgress++;
            ChangeMissiles(NuclearSide.UnitedStates, -4);
            ChangeMissiles(NuclearSide.SovietUnion, -4);
            return PeaceResult.Progress;
        }
        if (roll < 40) return PeaceResult.SurpriseAttack;
        return PeaceResult.NoProgress;
    }

    public void Surrender(NuclearSide side) => Winner = Opponent(side);

    public IReadOnlyList<StrikeReport> AiTurn(NuclearSide side)
    {
        if (Missiles(side) == 0) return Array.Empty<StrikeReport>();
        var targets = Cities.Where(c => c.Owner != side && c.Population > 0)
            .OrderByDescending(c => c.Population)
            .ThenBy(c => c.Name, StringComparer.Ordinal)
            .Take(MaxTargets(side))
            .Select(c => c.Name);
        return Launch(side, targets);
    }

    public static NuclearSide Opponent(NuclearSide side) => side == NuclearSide.UnitedStates ? NuclearSide.SovietUnion : NuclearSide.UnitedStates;

    private StrikeReport Fire(NuclearCity city)
    {
        var roll = _random.Next(100);
        var result = roll switch { < 10 => StrikeResult.Miss, < 30 => StrikeResult.Marginal, < 60 => StrikeResult.Minor, < 90 => StrikeResult.Major, _ => StrikeResult.Critical };
        var fraction = result switch { StrikeResult.Miss => 0d, StrikeResult.Marginal => .2d, StrikeResult.Minor => .4d, StrikeResult.Major => .6d, _ => 1d };
        var casualties = Math.Min(city.Population, (int)Math.Round(city.Population * fraction));
        city.Population -= casualties;
        city.LastImpact = result switch { StrikeResult.Miss => 'O', StrikeResult.Marginal => 'x', StrikeResult.Minor => '*', StrikeResult.Major => 'X', _ => '!' };
        return new StrikeReport(city.Name, result, casualties, city.Population, city.LastImpact);
    }

    private void ChangeMissiles(NuclearSide side, int delta)
    {
        if (side == NuclearSide.UnitedStates) UnitedStatesMissiles = Math.Max(0, UnitedStatesMissiles + delta);
        else SovietUnionMissiles = Math.Max(0, SovietUnionMissiles + delta);
    }

    private void EvaluateOutcome()
    {
        var us = Population(NuclearSide.UnitedStates);
        var ussr = Population(NuclearSide.SovietUnion);
        if (us == 0 && ussr == 0) IsDraw = true;
        else if (us == 0) Winner = NuclearSide.SovietUnion;
        else if (ussr == 0) Winner = NuclearSide.UnitedStates;
    }

    private static List<NuclearCity> CreateCities() =>
    [
        City("LAS VEGAS", NuclearSide.UnitedStates, 165_000), City("SEATTLE", NuclearSide.UnitedStates, 494_000),
        City("NEW YORK", NuclearSide.UnitedStates, 7_072_000), City("WASHINGTON DC", NuclearSide.UnitedStates, 638_000),
        City("CHICAGO", NuclearSide.UnitedStates, 3_005_000), City("MIAMI", NuclearSide.UnitedStates, 347_000),
        City("SAN FRANCISCO", NuclearSide.UnitedStates, 679_000),
        City("MOSCOW", NuclearSide.SovietUnion, 8_000_000), City("MINSK", NuclearSide.SovietUnion, 1_300_000),
        City("MURMANSK", NuclearSide.SovietUnion, 380_000), City("LENINGRAD", NuclearSide.SovietUnion, 4_600_000),
        City("KIEV", NuclearSide.SovietUnion, 2_100_000), City("CHELYABINSK", NuclearSide.SovietUnion, 1_030_000)
    ];

    private static NuclearCity City(string name, NuclearSide owner, int population)
        => new() { Name = name, Owner = owner, InitialPopulation = population, Population = population };
}
