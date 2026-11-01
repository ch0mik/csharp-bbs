using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bbs.Server.Adsb;

public sealed class AdsbPollingService(
    IAdsbFeedClient client,
    IAircraftSnapshotStore store,
    IOptions<AdsbRadarOptions> options,
    ILogger<AdsbPollingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        var failureCount = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan? retryAfter = null;
            try
            {
                var aircraft = await client.GetAircraftAsync(stoppingToken).ConfigureAwait(false);
                store.Set(new AircraftSnapshot(aircraft, DateTimeOffset.UtcNow, null));
                failureCount = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (AdsbRateLimitException exception)
            {
                failureCount++;
                retryAfter = exception.RetryAfter;
                store.Set(store.Current with { LastError = exception.Message });
                logger.LogWarning("ADS-B feed rate limited the server; retrying after {RetryAfter}.", retryAfter);
            }
            catch (Exception exception)
            {
                failureCount++;
                store.Set(store.Current with { LastError = exception.Message });
                logger.LogWarning(exception, "ADS-B feed request failed.");
            }
            var delay = CalculateDelay(options.Value.PollIntervalSeconds, failureCount, retryAfter);
            await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
        }
    }

    internal static TimeSpan CalculateDelay(int pollIntervalSeconds, int failureCount, TimeSpan? retryAfter)
    {
        var normalDelay = TimeSpan.FromSeconds(pollIntervalSeconds);
        if (retryAfter is { } value)
            return TimeSpan.FromSeconds(Math.Clamp(value.TotalSeconds, normalDelay.TotalSeconds, 120));
        if (failureCount == 0) return normalDelay;

        var backoffSeconds = Math.Min(60, normalDelay.TotalSeconds * Math.Pow(2, Math.Min(failureCount, 4)));
        var jitter = 0.8 + Random.Shared.NextDouble() * 0.4;
        return TimeSpan.FromSeconds(backoffSeconds * jitter);
    }
}
