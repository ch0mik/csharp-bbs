using Bbs.Tenants;
using Bbs.Tenants.Content.WarDialer;
using System.Text;

namespace Bbs.Tests;

public class WarDialerPetsciiTests
{
    [Fact]
    public void Catalog_FindsWoprAndDeduplicatesIt()
    {
        var state = new WarDialerSessionState();
        var wopr = state.Check(311, 437, 8739);

        Assert.NotNull(wopr);
        Assert.Equal("WOPR", wopr.Name);
        Assert.True(wopr.CanConnect);
        Assert.True(state.Add(wopr));
        Assert.False(state.Add(wopr));
        Assert.Single(state.Found);
        Assert.Null(state.Check(311, 437, 8738));
    }

    [Theory]
    [InlineData(311, 437, 8700, 8750, true)]
    [InlineData(0, 437, 8700, 8750, false)]
    [InlineData(311, 1000, 8700, 8750, false)]
    [InlineData(311, 437, 8750, 8700, false)]
    [InlineData(311, 437, 1, 300, false)]
    public void ScanRange_IsValidated(int area, int prefix, int start, int end, bool expected)
        => Assert.Equal(expected, WarDialerSessionState.IsValidScan(area, prefix, start, end));

    [Fact]
    public async Task DefaultScan_FindsAndConnectsToWopr()
    {
        var pair = await TestSocketPair.CreateAsync();
        using var server = pair.Server;
        using var client = pair.Client;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var bbs = new WarDialerPetscii { DialDelay = TimeSpan.FromMilliseconds(1) };
        var run = bbs.RunSessionAsync(server, TimeSpan.FromMinutes(1), cts.Token);
        var outputTask = ReadAllAsync(client.GetStream(), cts.Token);
        await Task.Delay(250, cts.Token);
        const string script = "s\r\r\r\r\r\rv\ra\r.\r.\r.\r";
        await client.GetStream().WriteAsync(Encoding.ASCII.GetBytes(script), cts.Token);
        await client.GetStream().FlushAsync(cts.Token);
        await run.WaitAsync(TimeSpan.FromSeconds(10), cts.Token);
        var output = await outputTask.WaitAsync(TimeSpan.FromSeconds(2), cts.Token);

        Assert.Contains("311-437-8739 CARRIER", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ATD3114378739", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("W.O.P.R. ONLINE", output, StringComparison.OrdinalIgnoreCase);
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
