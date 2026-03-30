using Bbs.Terminals;

namespace Bbs.Tests;

public class TerminalInputOutputTests
{
    [Fact]
    public async Task PetsciiInputOutput_ShouldExposeExpectedAliases()
    {
        var pair = await TestSocketPair.CreateAsync().ConfigureAwait(false);
        using var server = pair.Server;
        using var client = pair.Client;

        var io = new PetsciiInputOutput(server);
        Assert.Equal(10, io.ReturnAlias());
        Assert.Equal(8, io.BackspaceAlias());
        Assert.Equal(new byte[] { 13 }, io.NewlineBytes());
        Assert.True(io.IsNewline(13));
        Assert.True(io.IsBackspace(PetsciiKeys.Del));
    }

    [Fact]
    public async Task PetsciiInputOutput_ShouldConvertLetterCaseLikeOriginal()
    {
        var pair = await TestSocketPair.CreateAsync().ConfigureAwait(false);
        using var server = pair.Server;
        using var client = pair.Client;

        var io = new PetsciiInputOutput(server);
        Assert.Equal((int)'A', io.ConvertToAscii((int)'a'));
        Assert.Equal((int)'z', io.ConvertToAscii((int)'Z'));
        Assert.Equal((int)'A', io.ConvertToAscii(193));
        Assert.Equal((int)'_', io.ConvertToAscii(164));
        Assert.Equal(new byte[] { 13 }, io.NewlineBytes());
    }
}

