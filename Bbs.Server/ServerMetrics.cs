using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text;

namespace Bbs.Server;

internal static class ServerMetrics
{
    private sealed class BbsMetricState
    {
        public BbsMetricState(string endpoint, string tenant)
        {
            Endpoint = endpoint;
            Tenant = tenant;
        }

        public string Endpoint { get; }

        public string Tenant { get; }

        public long Started;

        public long Completed;

        public long Failed;

        public long Active;

        public long DurationCount;

        public double DurationSumMs;

        public double LastDurationMs;
    }

    private static readonly Meter Meter = new("CsharpBbs.Server", "1.0.0");
    private static readonly Counter<long> ConnectionsAcceptedCounter =
        Meter.CreateCounter<long>("bbs_connections_accepted_total", "{connection}", "Total accepted TCP connections.");
    private static readonly Counter<long> SessionsStartedCounter =
        Meter.CreateCounter<long>("bbs_sessions_started_total", "{session}", "Total started BBS sessions.");
    private static readonly Counter<long> SessionsCompletedCounter =
        Meter.CreateCounter<long>("bbs_sessions_completed_total", "{session}", "Total completed BBS sessions.");
    private static readonly Counter<long> SessionsFailedCounter =
        Meter.CreateCounter<long>("bbs_sessions_failed_total", "{session}", "Total failed BBS sessions.");
    private static readonly UpDownCounter<long> SessionsActiveCounter =
        Meter.CreateUpDownCounter<long>("bbs_sessions_active", "{session}", "Current active BBS sessions.");
    private static readonly Histogram<double> SessionDurationHistogram =
        Meter.CreateHistogram<double>("bbs_session_duration_ms", "ms", "BBS session duration in milliseconds.");
    private static readonly Counter<long> DiagnosticRequestsCounter =
        Meter.CreateCounter<long>("bbs_diagnostics_requests_total", "{request}", "Total diagnostic endpoint requests.");
    private static readonly Counter<long> MetricsRequestsCounter =
        Meter.CreateCounter<long>("bbs_metrics_requests_total", "{request}", "Total metrics endpoint requests.");

    private static long _connectionsAccepted;
    private static long _sessionsStarted;
    private static long _sessionsCompleted;
    private static long _sessionsFailed;
    private static long _sessionsActive;
    private static long _diagnosticRequests;
    private static long _metricsRequests;

    private static long _sessionDurationCount;
    private static double _sessionDurationSumMs;

    private static readonly ConcurrentDictionary<string, BbsMetricState> PerBbsStates = new(StringComparer.OrdinalIgnoreCase);

    static ServerMetrics()
    {
        Meter.CreateObservableGauge("bbs_clients_registered", ObserveRegisteredClients, "{client}", "Current registered clients in BBS registry.");
    }

    public static void ConnectionAccepted(string endpoint)
    {
        Interlocked.Increment(ref _connectionsAccepted);
        ConnectionsAcceptedCounter.Add(1, new KeyValuePair<string, object?>("endpoint", endpoint));
    }

    public static void SessionStarted(string endpoint, string tenant)
    {
        Interlocked.Increment(ref _sessionsStarted);
        Interlocked.Increment(ref _sessionsActive);

        var tags = BuildTags(endpoint, tenant);
        SessionsStartedCounter.Add(1, tags);
        SessionsActiveCounter.Add(1, tags);

        var state = GetState(endpoint, tenant);
        Interlocked.Increment(ref state.Started);
        Interlocked.Increment(ref state.Active);
    }

    public static void SessionCompleted(string endpoint, string tenant, TimeSpan duration)
    {
        Interlocked.Increment(ref _sessionsCompleted);
        Interlocked.Decrement(ref _sessionsActive);
        Interlocked.Increment(ref _sessionDurationCount);
        AddGlobalSessionDuration(duration.TotalMilliseconds);

        var tags = BuildTags(endpoint, tenant);
        SessionsCompletedCounter.Add(1, tags);
        SessionsActiveCounter.Add(-1, tags);
        SessionDurationHistogram.Record(duration.TotalMilliseconds, tags);

        var state = GetState(endpoint, tenant);
        Interlocked.Increment(ref state.Completed);
        Interlocked.Decrement(ref state.Active);
        Interlocked.Increment(ref state.DurationCount);
        AddStateSessionDuration(state, duration.TotalMilliseconds);
        SetStateLastDuration(state, duration.TotalMilliseconds);
    }

    public static void SessionFailed(string endpoint, string tenant, TimeSpan duration)
    {
        Interlocked.Increment(ref _sessionsFailed);
        Interlocked.Decrement(ref _sessionsActive);
        Interlocked.Increment(ref _sessionDurationCount);
        AddGlobalSessionDuration(duration.TotalMilliseconds);

        var tags = BuildTags(endpoint, tenant);
        SessionsFailedCounter.Add(1, tags);
        SessionsActiveCounter.Add(-1, tags);
        SessionDurationHistogram.Record(duration.TotalMilliseconds, tags);

        var state = GetState(endpoint, tenant);
        Interlocked.Increment(ref state.Failed);
        Interlocked.Decrement(ref state.Active);
        Interlocked.Increment(ref state.DurationCount);
        AddStateSessionDuration(state, duration.TotalMilliseconds);
        SetStateLastDuration(state, duration.TotalMilliseconds);
    }

    public static void DiagnosticRequest()
    {
        Interlocked.Increment(ref _diagnosticRequests);
        DiagnosticRequestsCounter.Add(1);
    }

    public static void MetricsRequest()
    {
        Interlocked.Increment(ref _metricsRequests);
        MetricsRequestsCounter.Add(1);
    }

    internal static void ResetForTests()
    {
        Interlocked.Exchange(ref _connectionsAccepted, 0);
        Interlocked.Exchange(ref _sessionsStarted, 0);
        Interlocked.Exchange(ref _sessionsCompleted, 0);
        Interlocked.Exchange(ref _sessionsFailed, 0);
        Interlocked.Exchange(ref _sessionsActive, 0);
        Interlocked.Exchange(ref _diagnosticRequests, 0);
        Interlocked.Exchange(ref _metricsRequests, 0);
        Interlocked.Exchange(ref _sessionDurationCount, 0);
        Interlocked.Exchange(ref _sessionDurationSumMs, 0);
        PerBbsStates.Clear();
    }

    public static string BuildOpenMetricsPayload()
    {
        var sb = new StringBuilder();

        AppendCounter(sb, "bbs_connections_accepted_total", Interlocked.Read(ref _connectionsAccepted));
        AppendCounter(sb, "bbs_sessions_started_total", Interlocked.Read(ref _sessionsStarted));
        AppendCounter(sb, "bbs_sessions_completed_total", Interlocked.Read(ref _sessionsCompleted));
        AppendCounter(sb, "bbs_sessions_failed_total", Interlocked.Read(ref _sessionsFailed));
        AppendGauge(sb, "bbs_sessions_active", Interlocked.Read(ref _sessionsActive));
        AppendGauge(sb, "bbs_clients_registered", Bbs.Core.BbsThread.Clients.Count);
        AppendCounter(sb, "bbs_diagnostics_requests_total", Interlocked.Read(ref _diagnosticRequests));
        AppendCounter(sb, "bbs_metrics_requests_total", Interlocked.Read(ref _metricsRequests));

        sb.AppendLine("# TYPE bbs_session_duration_ms summary");
        sb.Append("bbs_session_duration_ms_count ")
            .Append(Interlocked.Read(ref _sessionDurationCount).ToString(CultureInfo.InvariantCulture))
            .AppendLine();
        sb.Append("bbs_session_duration_ms_sum ")
            .Append(GetGlobalSessionDurationSum().ToString("0.###", CultureInfo.InvariantCulture))
            .AppendLine();

        AppendPerBbsCounters(sb, "bbs_sessions_started_by_bbs_total", "counter", s => Interlocked.Read(ref s.Started));
        AppendPerBbsCounters(sb, "bbs_sessions_completed_by_bbs_total", "counter", s => Interlocked.Read(ref s.Completed));
        AppendPerBbsCounters(sb, "bbs_sessions_failed_by_bbs_total", "counter", s => Interlocked.Read(ref s.Failed));
        AppendPerBbsCounters(sb, "bbs_sessions_active_by_bbs", "gauge", s => Interlocked.Read(ref s.Active));

        sb.AppendLine("# TYPE bbs_session_duration_ms_by_bbs summary");
        foreach (var state in PerBbsStates.Values.OrderBy(s => s.Endpoint, StringComparer.OrdinalIgnoreCase).ThenBy(s => s.Tenant, StringComparer.OrdinalIgnoreCase))
        {
            var labels = BuildLabelSet(state.Endpoint, state.Tenant);
            sb.Append("bbs_session_duration_ms_by_bbs_count")
                .Append(labels)
                .Append(' ')
                .Append(Interlocked.Read(ref state.DurationCount).ToString(CultureInfo.InvariantCulture))
                .AppendLine();
            sb.Append("bbs_session_duration_ms_by_bbs_sum")
                .Append(labels)
                .Append(' ')
                .Append(GetStateDurationSum(state).ToString("0.###", CultureInfo.InvariantCulture))
                .AppendLine();
        }

        AppendPerBbsDoubleGauge(sb, "bbs_last_session_duration_ms_by_bbs", s => GetStateLastDuration(s));

        sb.AppendLine("# EOF");
        return sb.ToString();
    }

    private static IEnumerable<Measurement<long>> ObserveRegisteredClients()
    {
        yield return new Measurement<long>(Bbs.Core.BbsThread.Clients.Count);
    }

    private static TagList BuildTags(string endpoint, string tenant)
    {
        return new TagList
        {
            { "endpoint", endpoint },
            { "tenant", tenant }
        };
    }

    private static string BuildLabelSet(string endpoint, string tenant)
    {
        return "{endpoint=\"" + EscapeLabelValue(endpoint) + "\",tenant=\"" + EscapeLabelValue(tenant) + "\"}";
    }

    private static void AppendCounter(StringBuilder sb, string name, long value)
    {
        sb.Append("# TYPE ").Append(name).AppendLine(" counter");
        sb.Append(name).Append(' ').Append(value.ToString(CultureInfo.InvariantCulture)).AppendLine();
    }

    private static void AppendGauge(StringBuilder sb, string name, long value)
    {
        sb.Append("# TYPE ").Append(name).AppendLine(" gauge");
        sb.Append(name).Append(' ').Append(value.ToString(CultureInfo.InvariantCulture)).AppendLine();
    }

    private static void AppendPerBbsCounters(StringBuilder sb, string name, string type, Func<BbsMetricState, long> selector)
    {
        sb.Append("# TYPE ").Append(name).Append(' ').AppendLine(type);
        foreach (var state in PerBbsStates.Values.OrderBy(s => s.Endpoint, StringComparer.OrdinalIgnoreCase).ThenBy(s => s.Tenant, StringComparer.OrdinalIgnoreCase))
        {
            sb.Append(name)
                .Append(BuildLabelSet(state.Endpoint, state.Tenant))
                .Append(' ')
                .Append(selector(state).ToString(CultureInfo.InvariantCulture))
                .AppendLine();
        }
    }

    private static void AppendPerBbsDoubleGauge(StringBuilder sb, string name, Func<BbsMetricState, double> selector)
    {
        sb.Append("# TYPE ").Append(name).AppendLine(" gauge");
        foreach (var state in PerBbsStates.Values.OrderBy(s => s.Endpoint, StringComparer.OrdinalIgnoreCase).ThenBy(s => s.Tenant, StringComparer.OrdinalIgnoreCase))
        {
            sb.Append(name)
                .Append(BuildLabelSet(state.Endpoint, state.Tenant))
                .Append(' ')
                .Append(selector(state).ToString("0.###", CultureInfo.InvariantCulture))
                .AppendLine();
        }
    }

    private static string EscapeLabelValue(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private static BbsMetricState GetState(string endpoint, string tenant)
    {
        var key = endpoint + "|" + tenant;
        return PerBbsStates.GetOrAdd(key, _ => new BbsMetricState(endpoint, tenant));
    }

    private static void AddGlobalSessionDuration(double value)
    {
        double initial;
        double computed;
        do
        {
            initial = _sessionDurationSumMs;
            computed = initial + value;
        }
        while (Interlocked.CompareExchange(ref _sessionDurationSumMs, computed, initial) != initial);
    }

    private static double GetGlobalSessionDurationSum()
    {
        return Interlocked.CompareExchange(ref _sessionDurationSumMs, 0, 0);
    }

    private static void AddStateSessionDuration(BbsMetricState state, double value)
    {
        double initial;
        double computed;
        do
        {
            initial = state.DurationSumMs;
            computed = initial + value;
        }
        while (Interlocked.CompareExchange(ref state.DurationSumMs, computed, initial) != initial);
    }

    private static double GetStateDurationSum(BbsMetricState state)
    {
        return Interlocked.CompareExchange(ref state.DurationSumMs, 0, 0);
    }

    private static void SetStateLastDuration(BbsMetricState state, double value)
    {
        Interlocked.Exchange(ref state.LastDurationMs, value);
    }

    private static double GetStateLastDuration(BbsMetricState state)
    {
        return Interlocked.CompareExchange(ref state.LastDurationMs, 0, 0);
    }
}
