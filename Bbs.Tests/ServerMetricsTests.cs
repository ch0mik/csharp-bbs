using Bbs.Server;

namespace Bbs.Tests;

public class ServerMetricsTests
{
    [Fact]
    public void BuildOpenMetricsPayload_ShouldContainExpectedMetrics()
    {
        ServerMetrics.ResetForTests();

        ServerMetrics.ConnectionAccepted("WelcomeBbs:6510");
        ServerMetrics.SessionStarted("WelcomeBbs:6510", "WelcomeBbs");
        ServerMetrics.SessionCompleted("WelcomeBbs:6510", "WelcomeBbs", TimeSpan.FromMilliseconds(123));
        ServerMetrics.DiagnosticRequest();
        ServerMetrics.MetricsRequest();

        var payload = ServerMetrics.BuildOpenMetricsPayload();

        Assert.Contains("bbs_connections_accepted_total", payload, StringComparison.Ordinal);
        Assert.Contains("bbs_sessions_started_total", payload, StringComparison.Ordinal);
        Assert.Contains("bbs_sessions_completed_total", payload, StringComparison.Ordinal);
        Assert.Contains("bbs_session_duration_ms_count", payload, StringComparison.Ordinal);
        Assert.Contains("bbs_sessions_active_by_bbs", payload, StringComparison.Ordinal);
    }
}
