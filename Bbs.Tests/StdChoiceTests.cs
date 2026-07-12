using Bbs.Server;
using System.Text;

namespace Bbs.Tests;

public class StdChoiceTests
{
    [Fact]
    public async Task StdChoice_ShouldShowCommodoreNewsOptionInMenu()
    {
        var pair = await TestSocketPair.CreateAsync().ConfigureAwait(false);
        using var server = pair.Server;
        using var client = pair.Client;

        var bbs = new StdChoice();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var sessionTask = bbs.RunSessionAsync(server, TimeSpan.FromMinutes(1), cts.Token);

        var stream = client.GetStream();
        await Task.Delay(300, cts.Token).ConfigureAwait(false);

        var captured = new List<byte>();
        var buffer = new byte[4096];
        string text;
        do
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cts.Token).ConfigureAwait(false);
            captured.AddRange(buffer.AsSpan(0, bytesRead).ToArray());
            text = Encoding.Latin1.GetString(captured.ToArray());
        }
        while (!text.Contains("Choice:", StringComparison.OrdinalIgnoreCase));

        Assert.Contains("C) Commodore News", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("W) WarGames Simulator", text, StringComparison.OrdinalIgnoreCase);

        await stream.WriteAsync(Encoding.ASCII.GetBytes("q\r"), cts.Token).ConfigureAwait(false);
        await stream.FlushAsync(cts.Token).ConfigureAwait(false);
        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5), cts.Token).ConfigureAwait(false);
    }

    [Theory]
    [InlineData("c", "C")]
    [InlineData("commodore", "COMMODORE")]
    [InlineData("news", "NEWS")]
    [InlineData("commodorenews", "COMMODORENEWS")]
    public void NormalizeChoice_ShouldPreserveCommodoreAliases(string input, string expected)
    {
        Assert.Equal(expected, StdChoice.NormalizeChoice(input));
    }

    [Theory]
    [InlineData("w", "W")]
    [InlineData("wopr", "WOPR")]
    [InlineData("wargames", "WARGAMES")]
    [InlineData("war games", "WAR GAMES")]
    public void NormalizeChoice_ShouldPreserveWoprAliases(string input, string expected)
        => Assert.Equal(expected, StdChoice.NormalizeChoice(input));

}
