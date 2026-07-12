using Bbs.Tenants;
using System.Text;

namespace Bbs.Tests;

public class WoprPetsciiTests
{
    [Fact]
    public async Task Session_CompletesCinematicPathAndReturns()
    {
        var pair = await TestSocketPair.CreateAsync();
        using var server = pair.Server;
        using var client = pair.Client;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var bbs = new WoprPetscii { AnimationDelay = TimeSpan.FromMilliseconds(1) };
        var sessionTask = bbs.RunSessionAsync(server, TimeSpan.FromMinutes(1), cts.Token);
        var outputTask = ReadAllAsync(client.GetStream(), cts.Token);

        await Task.Delay(250, cts.Token);
        const string script = "JOSHUA\rfine\ryes\ryes\r2\rlater\rc\r2\rmoscow\r\rl\r\r\r";
        await client.GetStream().WriteAsync(Encoding.ASCII.GetBytes(script), cts.Token);
        await client.GetStream().FlushAsync(cts.Token);

        await sessionTask.WaitAsync(TimeSpan.FromSeconds(25), cts.Token);
        var output = await outputTask.WaitAsync(TimeSpan.FromSeconds(2), cts.Token);

        Assert.Contains("GREETINGS PROFESSOR FALKEN", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GLOBAL THERMONUCLEAR WAR", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("C) CINEMATIC MODE", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("S) STRATEGIC SIMULATION", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DEFCON 1", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PROJECTED US LOSSES", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("STRATEGY ANALYSIS", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ALL OUTCOMES: NO WINNER", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("THE ONLY WINNING MOVE IS NOT TO PLAY", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DEFCON 5", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Logon_RejectsUnknownIdentityAndQuitReturns()
    {
        var pair = await TestSocketPair.CreateAsync();
        using var server = pair.Server;
        using var client = pair.Client;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var bbs = new WoprPetscii();
        var sessionTask = bbs.RunSessionAsync(server, TimeSpan.FromMinutes(1), cts.Token);
        var outputTask = ReadAllAsync(client.GetStream(), cts.Token);

        await Task.Delay(250, cts.Token);
        await client.GetStream().WriteAsync(Encoding.ASCII.GetBytes("FALKEN\r.\r"), cts.Token);
        await client.GetStream().FlushAsync(cts.Token);

        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5), cts.Token);
        var output = await outputTask.WaitAsync(TimeSpan.FromSeconds(2), cts.Token);
        Assert.Contains("IDENTIFICATION NOT RECOGNIZED", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Logon_HelpGamesListsFilmGameCatalog()
    {
        var pair = await TestSocketPair.CreateAsync();
        using var server = pair.Server;
        using var client = pair.Client;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var bbs = new WoprPetscii { AnimationDelay = TimeSpan.FromMilliseconds(1) };
        var sessionTask = bbs.RunSessionAsync(server, TimeSpan.FromMinutes(1), cts.Token);
        var outputTask = ReadAllAsync(client.GetStream(), cts.Token);

        await Task.Delay(250, cts.Token);
        await client.GetStream().WriteAsync(Encoding.ASCII.GetBytes("Help Games\r.\r"), cts.Token);
        await client.GetStream().FlushAsync(cts.Token);
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5), cts.Token);
        var output = await outputTask.WaitAsync(TimeSpan.FromSeconds(2), cts.Token);

        Assert.Contains("FALKEN'S MAZE", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BIOTOXIC AND CHEMICAL WARFARE", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GLOBAL THERMONUCLEAR WAR", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InterruptedGame_IsOfferedForResume()
    {
        var bbs = new WoprPetscii();

        var firstPair = await TestSocketPair.CreateAsync();
        using (firstPair.Server)
        using (firstPair.Client)
        using (var firstCts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            var firstRun = bbs.RunSessionAsync(firstPair.Server, TimeSpan.FromMinutes(1), firstCts.Token);
            await Task.Delay(250, firstCts.Token);
            await firstPair.Client.GetStream().WriteAsync(Encoding.ASCII.GetBytes("JOSHUA\r.\r"), firstCts.Token);
            await firstPair.Client.GetStream().FlushAsync(firstCts.Token);
            await firstRun.WaitAsync(TimeSpan.FromSeconds(5), firstCts.Token);
        }

        var secondPair = await TestSocketPair.CreateAsync();
        using (secondPair.Server)
        using (secondPair.Client)
        using (var secondCts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            var secondRun = bbs.RunSessionAsync(secondPair.Server, TimeSpan.FromMinutes(1), secondCts.Token);
            var outputTask = ReadAllAsync(secondPair.Client.GetStream(), secondCts.Token);
            await Task.Delay(250, secondCts.Token);
            await secondPair.Client.GetStream().WriteAsync(Encoding.ASCII.GetBytes("N\r.\r"), secondCts.Token);
            await secondPair.Client.GetStream().FlushAsync(secondCts.Token);
            await secondRun.WaitAsync(TimeSpan.FromSeconds(5), secondCts.Token);
            var output = await outputTask.WaitAsync(TimeSpan.FromSeconds(2), secondCts.Token);
            Assert.Contains("WOPR SESSION INTERRUPTED", output, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("R) RESUME", output, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ImportantRenderedLines_FitPetsciiWidth()
    {
        string[] lines =
        [
            "THE ONLY WINNING MOVE IS NOT TO PLAY.",
            "GAMES REFER TO MODELS, SIMULATIONS",
            "COUNTY NAME. BLANK LINE ENDS LIST.",
            " `---._____.---.____|____.---.__.'"
        ];

        Assert.All(lines, line => Assert.True(line.Length <= 39, $"Line too wide: {line}"));
    }

    [Fact]
    public void LearningDelay_AcceleratesAndKeepsReadableFloor()
    {
        var bbs = new WoprPetscii { AnimationDelay = TimeSpan.FromMilliseconds(800) };

        Assert.Equal(TimeSpan.FromMilliseconds(800), bbs.GetLearningDelay(1));
        Assert.True(bbs.GetLearningDelay(8) < bbs.GetLearningDelay(2));
        Assert.True(bbs.GetLearningDelay(24) < bbs.GetLearningDelay(8));
        Assert.True(bbs.GetLearningDelay(24) >= TimeSpan.FromMilliseconds(20));
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
