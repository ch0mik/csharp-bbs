using Bbs.Tenants;
using System.Text;

namespace Bbs.Tests;

public class WarGamesPetsciiTests
{
    [Fact]
    public async Task SingleTenant_ConnectsToSchoolAndReturnsToImsai()
    {
        var output = await RunAsync("1\rPENCIL\r.\r.\r");

        Assert.Contains("IMSAI 8080 COMMUNICATIONS", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DIALING", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SEATTLE PUBLIC", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PASSWORD VERIFIED", output, StringComparison.OrdinalIgnoreCase);
        Assert.True(Count(output, "IMSAI 8080 COMMUNICATIONS") >= 2);
    }

    [Fact]
    public async Task SingleTenant_OpensWarDialerAndReturnsToImsai()
    {
        var output = await RunAsync("2\r.\r.\r");

        Assert.Contains("WARGAMES MODEM DIALER", output, StringComparison.OrdinalIgnoreCase);
        Assert.True(Count(output, "IMSAI 8080 COMMUNICATIONS") >= 2);
    }

    [Fact]
    public async Task SingleTenant_ExposesStrategicSimulationDirectly()
    {
        var output = await RunAsync("3\r.\r.\r");

        Assert.Contains("STRATEGIC GTW SIMULATION", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SIMULATION MODE", output, StringComparison.OrdinalIgnoreCase);
        Assert.True(Count(output, "IMSAI 8080 COMMUNICATIONS") >= 2);
    }

    private static int Count(string value, string fragment)
    {
        var count = 0;
        var offset = 0;
        while ((offset = value.IndexOf(fragment, offset, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            offset += fragment.Length;
        }
        return count;
    }

    private static async Task<string> RunAsync(string script)
    {
        var pair = await TestSocketPair.CreateAsync();
        using var server = pair.Server;
        using var client = pair.Client;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var run = new WarGamesPetscii().RunSessionAsync(server, TimeSpan.FromMinutes(1), cts.Token);
        var outputTask = ReadAllAsync(client.GetStream(), cts.Token);
        await Task.Delay(250, cts.Token);
        await client.GetStream().WriteAsync(Encoding.ASCII.GetBytes(script), cts.Token);
        await client.GetStream().FlushAsync(cts.Token);
        await run.WaitAsync(TimeSpan.FromSeconds(7), cts.Token);
        return await outputTask.WaitAsync(TimeSpan.FromSeconds(2), cts.Token);
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
