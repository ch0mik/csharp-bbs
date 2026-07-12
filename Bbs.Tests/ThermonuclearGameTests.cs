using Bbs.Tenants.Content.Thermonuclear;

namespace Bbs.Tests;

public class ThermonuclearGameTests
{
    [Fact]
    public void Launch_UsesMissilesAndRejectsFriendlyOrUnknownTargets()
    {
        var game = new ThermonuclearGame(seed: 7);

        var reports = game.Launch(NuclearSide.UnitedStates, ["MOSCOW", "CHICAGO", "UNKNOWN"]);

        Assert.Single(reports);
        Assert.Equal("MOSCOW", reports[0].Target);
        Assert.Equal(31, game.UnitedStatesMissiles);
        Assert.InRange(reports[0].Marker, '!', 'x');
    }

    [Fact]
    public void Launch_RespectsDifferentTargetLimits()
    {
        var us = new ThermonuclearGame(seed: 1);
        var ussr = new ThermonuclearGame(seed: 1);
        var sovietTargets = us.Cities.Where(c => c.Owner == NuclearSide.SovietUnion).Select(c => c.Name);
        var usTargets = ussr.Cities.Where(c => c.Owner == NuclearSide.UnitedStates).Select(c => c.Name);

        Assert.Equal(4, us.Launch(NuclearSide.UnitedStates, sovietTargets).Count);
        Assert.Equal(6, ussr.Launch(NuclearSide.SovietUnion, usTargets).Count);
    }

    [Fact]
    public void Surrender_HandsVictoryToOpponent()
    {
        var game = new ThermonuclearGame(seed: 1);

        game.Surrender(NuclearSide.UnitedStates);

        Assert.True(game.IsOver);
        Assert.Equal(NuclearSide.SovietUnion, game.Winner);
    }

    [Fact]
    public void AiTurn_TargetsEnemyCities()
    {
        var game = new ThermonuclearGame(seed: 3);

        var reports = game.AiTurn(NuclearSide.SovietUnion);

        Assert.NotEmpty(reports);
        Assert.All(reports, report => Assert.Contains(game.Cities, c => c.Name == report.Target && c.Owner == NuclearSide.UnitedStates));
    }
}
