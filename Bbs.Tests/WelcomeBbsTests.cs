using Bbs.Server;
using System.Text;

namespace Bbs.Tests;

public class WelcomeBbsTests
{
    [Fact]
    public async Task WelcomeBbs_ShouldKeepSessionAliveUntilQuitCommand()
    {
        var pair = await TestSocketPair.CreateAsync().ConfigureAwait(false);
        using var server = pair.Server;
        using var client = pair.Client;

        var bbs = new WelcomeBbs();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var sessionTask = bbs.RunSessionAsync(server, TimeSpan.FromMinutes(1), cts.Token);

        var stream = client.GetStream();

        await stream.WriteAsync(Encoding.ASCII.GetBytes("Jan\r")).ConfigureAwait(false);
        await stream.FlushAsync().ConfigureAwait(false);
        await Task.Delay(300).ConfigureAwait(false);

        Assert.False(sessionTask.IsCompleted);

        await stream.WriteAsync(Encoding.ASCII.GetBytes("/quit\r")).ConfigureAwait(false);
        await stream.FlushAsync().ConfigureAwait(false);

        await sessionTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        Assert.True(sessionTask.IsCompletedSuccessfully);
    }
}
