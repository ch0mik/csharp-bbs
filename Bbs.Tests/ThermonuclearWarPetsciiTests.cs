using Bbs.Tenants;
using System.Text;

namespace Bbs.Tests;

public class ThermonuclearWarPetsciiTests
{
    [Fact]
    public async Task Simulation_AllowsStrikeAndReturnToWopr()
    {
        var pair = await TestSocketPair.CreateAsync();
        using var server = pair.Server;
        using var client = pair.Client;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var run = new ThermonuclearWarPetscii().RunSessionAsync(server, TimeSpan.FromMinutes(1), cts.Token);
        var outputTask = ReadAllAsync(client.GetStream(), cts.Token);
        await Task.Delay(250, cts.Token);
        await client.GetStream().WriteAsync(Encoding.ASCII.GetBytes("1\r1\r1\r\r.\r"), cts.Token);
        await client.GetStream().FlushAsync(cts.Token);
        await run.WaitAsync(TimeSpan.FromSeconds(5), cts.Token);
        var output = await outputTask.WaitAsync(TimeSpan.FromSeconds(2), cts.Token);

        Assert.Contains("SIMULATION MODE", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("YOUR STRIKE", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WOPR RETALIATION", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ICBM US:", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1) MOSCOW", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TargetParser_AcceptsNumbersAndNamesAndRemovesDuplicates()
    {
        var game = new Bbs.Tenants.Content.Thermonuclear.ThermonuclearGame(seed: 1);
        var enemies = game.Cities
            .Where(c => c.Owner == Bbs.Tenants.Content.Thermonuclear.NuclearSide.SovietUnion)
            .ToArray();

        var targets = ThermonuclearWarPetscii.ParseTargets("1, minsk, 1, bad", enemies);

        Assert.Equal(["MOSCOW", "MINSK"], targets);
    }

    private static async Task<string> ReadAllAsync(Stream stream, CancellationToken token)
    {
        var bytes = new List<byte>();
        var buffer = new byte[4096];
        while (true)
        {
            var count = await stream.ReadAsync(buffer, token);
            if (count == 0) break;
            bytes.AddRange(buffer.AsSpan(0, count).ToArray());
        }
        return Encoding.Latin1.GetString(bytes.ToArray());
    }
}
