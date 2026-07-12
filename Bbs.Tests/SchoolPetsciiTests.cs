using Bbs.Tenants;
using Bbs.Tenants.Content.School;
using System.Text;

namespace Bbs.Tests;

public class SchoolPetsciiTests
{
    [Theory]
    [InlineData("PENCIL")]
    [InlineData("pencil")]
    [InlineData(" Pencil ")]
    public void Password_IsCaseInsensitive(string password)
        => Assert.True(SchoolInput.IsPasswordValid(password));

    [Fact]
    public void State_SearchesAndValidatesGradeChanges()
    {
        var state = new SchoolSessionState();
        var records = state.Search("lightman");

        Assert.Equal(6, records.Count);
        Assert.True(state.TryChangeGrade(records, "s-202", "a"));
        Assert.Equal('A', records.Single(r => r.ClassNumber == "S-202").Grade);
        Assert.False(state.TryChangeGrade(records, "BAD", "A"));
        Assert.False(state.TryChangeGrade(records, "S-202", "X"));
    }

    [Fact]
    public async Task Session_ChangesGradeAndKeepsItForNextConnection()
    {
        var bbs = new SchoolPetscii();
        var firstOutput = await RunSessionAsync(bbs, "pencil\rlightman\re\rs-202\ra\r\r.\r");

        Assert.Contains("PASSWORD VERIFIED", firstOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("STUDENT RECORD UPDATED", firstOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("S-202  BIOLOGY 2          A", firstOutput, StringComparison.OrdinalIgnoreCase);

        var secondOutput = await RunSessionAsync(bbs, "PENCIL\rLIGHTMAN\r.\r");
        Assert.Contains("S-202  BIOLOGY 2          A", secondOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Session_DisconnectsAfterThreeBadPasswords()
    {
        var output = await RunSessionAsync(new SchoolPetscii(), "bad\rwrong\rnope\r");

        Assert.Contains("TOO MANY INVALID PASSWORD ATTEMPTS", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--DISCONNECTED--", output, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> RunSessionAsync(SchoolPetscii bbs, string script)
    {
        var pair = await TestSocketPair.CreateAsync();
        using var server = pair.Server;
        using var client = pair.Client;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var run = bbs.RunSessionAsync(server, TimeSpan.FromMinutes(1), cts.Token);
        var outputTask = ReadAllAsync(client.GetStream(), cts.Token);
        await Task.Delay(250, cts.Token);
        await client.GetStream().WriteAsync(Encoding.ASCII.GetBytes(script), cts.Token);
        await client.GetStream().FlushAsync(cts.Token);
        await run.WaitAsync(TimeSpan.FromSeconds(5), cts.Token);
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
