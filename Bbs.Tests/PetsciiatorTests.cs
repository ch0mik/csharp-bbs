using Bbs.Petsciiator;
using System.Net;
using System.Net.Http;

namespace Bbs.Tests;

public class PetsciiatorTests
{
    private static readonly byte[] Png1x1 = File.ReadAllBytes(GetPngPath());

    private static readonly byte[] Gif1x1 = Convert.FromHexString(
        "47494638396101000100800000FFFFFF00000021F90401000000002C00000000010001000002024401003B");

    [Fact]
    public async Task ConvertAsync_ShouldHandlePngBytes()
    {
        using var converter = new PetsciiatorConverter();

        var result = await converter.ConvertAsync(Png1x1).ConfigureAwait(false);

        Assert.True(result.Length > 900);
        Assert.Equal(147, result[0]);
        Assert.Equal(5, result[1]);
    }

    [Fact]
    public async Task ConvertAsync_ShouldHandleGifBytes()
    {
        using var converter = new PetsciiatorConverter();

        var result = await converter.ConvertAsync(Gif1x1).ConfigureAwait(false);

        Assert.True(result.Length > 900);
        Assert.Equal(147, result[0]);
        Assert.Equal(5, result[1]);
    }

    [Fact]
    public async Task ConvertAsync_RawMode_ShouldKeepLineBreaks()
    {
        using var converter = new PetsciiatorConverter();
        var options = new PetsciiatorOptions { BbsCompatibleOutput = false };

        var result = await converter.ConvertAsync(Png1x1, options).ConfigureAwait(false);

        Assert.Equal(1025, result.Length);
        Assert.Equal(25, result.Count(b => b == 13));
    }

    [Fact]
    public async Task ConvertFromUrlAsync_ShouldFetchImageAndConvert()
    {
        using var client = new HttpClient(new StubImageHandler(Png1x1));
        using var converter = new PetsciiatorConverter(client);

        var result = await converter.ConvertFromUrlAsync("example.test/image.png").ConfigureAwait(false);

        Assert.True(result.Length > 900);
        Assert.Equal(147, result[0]);
        Assert.Equal(5, result[1]);
    }

    private sealed class StubImageHandler : HttpMessageHandler
    {
        private readonly byte[] _content;

        public StubImageHandler(byte[] content)
        {
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_content)
            };

            return Task.FromResult(response);
        }
    }

    private static string GetPngPath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Bbs.Core",
            "Assets",
            "petscii_low.png"));
    }
}
