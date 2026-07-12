using Bbs.Terminals;
using Bbs.Tenants.Content.Thermonuclear;

namespace Bbs.Tenants;

public sealed class ThermonuclearWarPetscii : PetsciiThread
{
    public override async Task DoLoopAsync(CancellationToken cancellationToken = default)
    {
        var game = new ThermonuclearGame();
        var human = await SelectSideAsync(cancellationToken).ConfigureAwait(false);
        if (human is null) return;
        var ai = ThermonuclearGame.Opponent(human.Value);

        while (!cancellationToken.IsCancellationRequested && !game.IsOver)
        {
            DrawStatus(game, human.Value);
            Println("1) MISSILE LAUNCH");
            Println("2) PEACE TALKS");
            Println("3) SURRENDER");
            Println("4) NOTHING");
            Println(".) RETURN TO WOPR");
            Print("ACTION: ");
            var action = Normalize(await ReadAsync(8, cancellationToken).ConfigureAwait(false));
            if (action is "." or "QUIT" or "EXIT") return;

            var aiActs = true;
            if (action is "1" or "LAUNCH")
            {
                var enemyCities = game.Cities
                    .Where(c => c.Owner != human.Value && c.Population > 0)
                    .ToArray();
                Println($"SELECT UP TO {game.MaxTargets(human.Value)} ENEMY CITIES");
                for (var i = 0; i < enemyCities.Length; i++)
                {
                    Println($"{i + 1}) {enemyCities[i].Name}");
                }
                Println("USE NUMBERS OR CITY NAMES");
                Print("TARGETS (E.G. 1,2): ");
                var targetInput = await ReadAsync(39, cancellationToken).ConfigureAwait(false);
                var targets = ParseTargets(targetInput, enemyCities);
                var reports = game.Launch(human.Value, targets);
                if (reports.Count == 0)
                {
                    Println("NO VALID ENEMY CITY SELECTED");
                    Println("CHOOSE A NUMBER OR NAME FROM LIST");
                    await WaitAsync(cancellationToken).ConfigureAwait(false);
                    continue;
                }
                ShowReports("YOUR STRIKE", reports);
            }
            else if (action is "2" or "PEACE")
            {
                var result = game.Negotiate(human.Value);
                Println(result switch
                {
                    PeaceResult.Progress => "PEACE TALKS: PROGRESS",
                    PeaceResult.SurpriseAttack => "PEACE TALKS: SURPRISE ATTACK",
                    _ => "PEACE TALKS: NO PROGRESS"
                });
                aiActs = result != PeaceResult.Progress;
            }
            else if (action is "3" or "SURRENDER")
            {
                game.Surrender(human.Value);
                break;
            }
            else if (action is not ("4" or "NOTHING"))
            {
                Println("INVALID ACTION");
                await WaitAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (!game.IsOver && aiActs)
            {
                var reports = game.AiTurn(ai);
                if (reports.Count > 0) ShowReports("WOPR RETALIATION", reports);
            }
            if (!game.IsOver) await WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        Cls();
        Println("SIMULATION COMPLETE");
        Println("---------------------------------------");
        if (game.PeaceProgress >= 5) Println("PEACE AGREEMENT ACHIEVED");
        else if (game.IsDraw) Println("WINNER: NONE");
        else Println($"WINNER: {SideName(game.Winner!.Value)}");
        Println();
        Println("A STRANGE GAME.");
        Println("THE ONLY WINNING MOVE IS NOT TO PLAY.");
        await WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<NuclearSide?> SelectSideAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Cls();
            Println("GLOBAL THERMONUCLEAR WAR");
            Println("SIMULATION MODE");
            Println("---------------------------------------");
            Println("1) UNITED STATES");
            Println("2) SOVIET UNION");
            Println(".) RETURN TO WOPR");
            Print("SIDE: ");
            var input = Normalize(await ReadAsync(16, token).ConfigureAwait(false));
            if (input is "." or "QUIT") return null;
            if (input is "1" or "USA" or "UNITED STATES") return NuclearSide.UnitedStates;
            if (input is "2" or "USSR" or "SOVIET UNION") return NuclearSide.SovietUnion;
        }
        return null;
    }

    private void DrawStatus(ThermonuclearGame game, NuclearSide human)
    {
        Cls();
        Println("GTW COMMAND STATUS");
        Println($"YOU: {SideName(human)}");
        Println($"ICBM US:{game.UnitedStatesMissiles} USSR:{game.SovietUnionMissiles}");
        Println($"PEACE PROGRESS: {game.PeaceProgress}/5");
        Println("---------------------------------------");
        foreach (var city in game.Cities)
        {
            var side = city.Owner == NuclearSide.UnitedStates ? "US" : "SU";
            Println($"{city.LastImpact} {side} {city.Name,-13} {city.Population,8}");
        }
        Println("---------------------------------------");
    }

    private void ShowReports(string title, IReadOnlyList<StrikeReport> reports)
    {
        Println();
        Println(title);
        foreach (var report in reports)
        {
            Println($"{report.Marker} {report.Target}: {report.Result}");
            Println($"  LOSSES {report.Casualties}");
        }
    }

    private async Task<string> ReadAsync(int maxLength, CancellationToken token)
    {
        await FlushAsync(token).ConfigureAwait(false);
        return await ReadLineAsync(maxLength, token).ConfigureAwait(false);
    }

    private async Task WaitAsync(CancellationToken token)
    {
        Print("PRESS ENTER: ");
        await ReadAsync(1, token).ConfigureAwait(false);
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
    private static string SideName(NuclearSide side) => side == NuclearSide.UnitedStates ? "UNITED STATES" : "SOVIET UNION";

    internal static IReadOnlyList<string> ParseTargets(string input, IReadOnlyList<NuclearCity> enemyCities)
    {
        var result = new List<string>();
        foreach (var token in (input ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(token, out var number) && number >= 1 && number <= enemyCities.Count)
            {
                result.Add(enemyCities[number - 1].Name);
                continue;
            }
            var city = enemyCities.FirstOrDefault(c => string.Equals(c.Name, token, StringComparison.OrdinalIgnoreCase));
            if (city is not null) result.Add(city.Name);
        }
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
