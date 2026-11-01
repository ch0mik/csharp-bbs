using System.Net;
using System.Text;
using Bbs.Server.Adsb;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bbs.Tests;

public sealed class CitySearchTests
{
    [Fact]
    public async Task SearchEncodesQueryAndRejectsInvalidCoordinatesAndTerminalControls()
    {
        var handler = new StubHandler("""
            {"results":[
              {"name":"Łódź\u001b\r\n","country":"Poland","admin1":"Łódzkie","admin2":"Łódź County","country_code":"PL","latitude":51.7592,"longitude":19.456},
              {"name":"Invalid","latitude":91,"longitude":20},
              {"name":"Wrong type","latitude":"51","longitude":20}
            ]}
            """);
        var results = await CreateService(handler).SearchAsync("Lodz &count=100", CancellationToken.None);
        var result = Assert.Single(results!);
        Assert.Equal("Lodz", result.Name);
        Assert.Equal("Lodzkie", result.Region);
        Assert.Equal("Poland", result.Country);
        Assert.Equal("Lodz County", result.County);
        Assert.Equal(51.7592, result.Latitude);
        Assert.Contains("name=Lodz%20%26count%3D100&count=5", handler.RequestUri!.AbsoluteUri);
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("City\u001b[2J")]
    public async Task InvalidQueryMakesNoHttpRequest(string query)
    {
        var handler = new StubHandler("{}");
        Assert.Empty((await CreateService(handler).SearchAsync(query, CancellationToken.None))!);
        Assert.Null(handler.RequestUri);
    }

    [Fact]
    public async Task OversizedQueryMakesNoHttpRequest()
    {
        var handler = new StubHandler("{}");
        Assert.Empty((await CreateService(handler).SearchAsync(new string('a', 65), CancellationToken.None))!);
        Assert.Null(handler.RequestUri);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"results\":{}}")]
    public async Task InvalidProviderResponseReturnsUnavailable(string json)
        => Assert.Null(await CreateService(new StubHandler(json)).SearchAsync("Warsaw", CancellationToken.None));

    [Fact]
    public async Task OversizedResponseReturnsUnavailable()
        => Assert.Null(await CreateService(new StubHandler(new string(' ', 65537))).SearchAsync("Warsaw", CancellationToken.None));

    [Fact]
    public async Task RateLimitReturnsUnavailable()
        => Assert.Null(await CreateService(new StubHandler("{}", HttpStatusCode.TooManyRequests)).SearchAsync("Warsaw", CancellationToken.None));

    [Fact]
    public async Task NoResultsReturnsEmptyList()
        => Assert.Empty((await CreateService(new StubHandler("{}")).SearchAsync("Unknown", CancellationToken.None))!);

    [Fact]
    public async Task MissingAdministrativeFieldsRemainEmptyAndCountryFallsBackToCode()
    {
        var results = await CreateService(new StubHandler("""
            {"results":[{"name":"Village","country_code":"GB","latitude":52,"longitude":0}]}
            """)).SearchAsync("Village", CancellationToken.None);
        var result = Assert.Single(results!);
        Assert.Equal("GB", result.Country);
        Assert.Empty(result.Region);
        Assert.Empty(result.County);
    }

    [Fact]
    public async Task CallerCancellationPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateService(new StubHandler("{}")).SearchAsync("Warsaw", cancellation.Token));
    }

    [Theory]
    [InlineData("http://example.com/search", 5)]
    [InlineData("https://user:password@example.com/search", 5)]
    [InlineData("https://example.com/search?name=x", 5)]
    [InlineData("https://example.com/search", 0)]
    public void UnsafeConfigurationRejected(string endpoint, int timeout)
        => Assert.True(new CitySearchOptionsValidator().Validate(null, new CitySearchOptions { Endpoint = endpoint, RequestTimeoutSeconds = timeout }).Failed);

    private static CitySearchService CreateService(StubHandler handler)
        => new(new ClientFactory(handler), Options.Create(new CitySearchOptions()), NullLogger<CitySearchService>.Instance);

    private sealed class ClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubHandler(string json, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
}
