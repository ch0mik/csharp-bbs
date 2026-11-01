using Bbs.Core;
using Bbs.Server.Adsb;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace Bbs.Server;

internal static class Program
{
    private sealed record EndPointSpec(Type BbsType, int Port);

    private sealed class SessionTracker
    {
        private readonly ConcurrentDictionary<long, Task> _sessions = new();
        private long _nextId;

        public long NextId() => Interlocked.Increment(ref _nextId);
        public void Add(long id, Task task) => _sessions[id] = task;
        public void Remove(long id) => _sessions.TryRemove(id, out _);

        public async Task WaitForCompletionAsync(TimeSpan timeout)
        {
            var sessions = _sessions.Values.ToArray();
            if (sessions.Length == 0) return;
            try
            {
                await Task.WhenAll(sessions).WaitAsync(timeout).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                Console.Error.WriteLine($"Timed out waiting for {_sessions.Count} BBS session(s) to stop.");
            }
        }
    }

    private static Dictionary<string, Type> TenantMap = new(StringComparer.OrdinalIgnoreCase);
    private static RedisSessionStore? SessionStore;
    public static async Task<int> Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.Services
            .AddOptions<AdsbRadarOptions>()
            .Bind(builder.Configuration.GetSection(AdsbRadarOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<AdsbRadarOptions>, AdsbRadarOptionsValidator>();
        builder.Services
            .AddOptions<ClientGeolocationOptions>()
            .Bind(builder.Configuration.GetSection(ClientGeolocationOptions.SectionName))
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<ClientGeolocationOptions>, ClientGeolocationOptionsValidator>();
        builder.Services.AddHttpClient("adsb", (serviceProvider, client) =>
        {
            var radar = serviceProvider.GetRequiredService<IOptions<AdsbRadarOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(radar.RequestTimeoutSeconds);
        });
        builder.Services.AddSingleton<IAdsbFeedClient, AdsbFeedClient>();
        builder.Services.AddSingleton<IAircraftSnapshotStore, AircraftSnapshotStore>();
        builder.Services.AddSingleton<AdsbRadarSessionLimiter>();
        builder.Services.AddHostedService<AdsbPollingService>();
        builder.Services.AddHttpClient("client-geolocation", (serviceProvider, client) =>
        {
            var geolocation = serviceProvider.GetRequiredService<IOptions<ClientGeolocationOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(geolocation.RequestTimeoutSeconds);
        });
        builder.Services.AddSingleton<IClientGeolocationService, ClientGeolocationService>();

        using var host = builder.Build();
        try
        {
            _ = host.Services.GetRequiredService<IOptions<AdsbRadarOptions>>().Value;
            _ = host.Services.GetRequiredService<IOptions<ClientGeolocationOptions>>().Value;
        }
        catch (OptionsValidationException exception)
        {
            foreach (var failure in exception.Failures) Console.Error.WriteLine($"Configuration error: {failure}");
            return 2;
        }

        LoadReferencedAssemblies();
        TenantMap = DiscoverTenants();

        if (!TryParseArguments(args, out var endpoints, out var timeout, out var servicePort, out var showHelp, out var errors))
        {
            foreach (var error in errors)
            {
                Console.Error.WriteLine(error);
            }

            DisplayHelp();
            return 2;
        }

        if (showHelp || endpoints.Count == 0)
        {
            DisplayHelp();
            return 1;
        }

        ConfigureSessionStore();
        await host.StartAsync().ConfigureAwait(false);

        using var cts = new CancellationTokenSource();
        using var stoppingRegistration = host.Services
            .GetRequiredService<IHostApplicationLifetime>()
            .ApplicationStopping
            .Register(cts.Cancel);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        Console.WriteLine($"Starting BBS endpoints: {string.Join(", ", endpoints.Select(x => $"{x.BbsType.Name}:{x.Port}"))}");
        Console.WriteLine($"Timeout: {timeout.TotalMilliseconds} ms");
        if (servicePort > 0)
        {
            Console.WriteLine($"Service port: {servicePort}");
        }

        var tasks = new List<Task>();
        var sessionTracker = new SessionTracker();
        foreach (var endpoint in endpoints)
        {
            tasks.Add(Task.Run(() => RunEndpointAsync(endpoint, timeout, host.Services, sessionTracker, cts.Token), cts.Token));
        }

        if (servicePort > 0)
        {
            tasks.Add(Task.Run(() => RunServicePortAsync(servicePort, sessionTracker, cts.Token), cts.Token));
        }

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        finally
        {
            await sessionTracker.WaitForCompletionAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            await host.StopAsync().ConfigureAwait(false);
        }
        return 0;
    }


    private static void ConfigureSessionStore()
    {
        var modeRaw = Environment.GetEnvironmentVariable("BBS_SESSION_STORE");
        var mode = string.IsNullOrWhiteSpace(modeRaw) ? "inmemory" : modeRaw.Trim().ToLowerInvariant();

        if (mode is "inmemory" or "memory" or "proc")
        {
            SessionStore = null;
            SessionLifecycleHooks.OnSessionStarted = null;
            SessionLifecycleHooks.OnSessionEnded = null;
            Console.WriteLine("Session store mode: in-memory (process local)");
            return;
        }

        if (mode is not "redis")
        {
            SessionStore = null;
            SessionLifecycleHooks.OnSessionStarted = null;
            SessionLifecycleHooks.OnSessionEnded = null;
            Console.WriteLine($"Session store mode '{mode}' is unknown. Falling back to in-memory.");
            return;
        }

        SessionStore = RedisSessionStore.CreateFromEnvironment();
        if (SessionStore is null)
        {
            Console.WriteLine("Session store mode: redis requested, but REDIS_HOST/REDIS_PASSWORD are missing. Falling back to in-memory.");
            SessionLifecycleHooks.OnSessionStarted = null;
            SessionLifecycleHooks.OnSessionEnded = null;
            return;
        }

        Console.WriteLine("Session store mode: redis");
        SessionLifecycleHooks.OnSessionStarted = thread => SessionStore?.UpsertActiveSession(thread);
        SessionLifecycleHooks.OnSessionEnded = thread => SessionStore?.RemoveActiveSession(thread.ClientId);
    }

    private static void LoadReferencedAssemblies()
    {
        var entry = Assembly.GetEntryAssembly();
        if (entry is null)
        {
            return;
        }

        foreach (var reference in entry.GetReferencedAssemblies())
        {
            try
            {
                Assembly.Load(reference);
            }
            catch
            {
                // ignore optional assemblies
            }
        }
    }

    private static async Task RunEndpointAsync(
        EndPointSpec endpoint,
        TimeSpan timeout,
        IServiceProvider services,
        SessionTracker sessionTracker,
        CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Any, endpoint.Port);
        listener.Start();
        Console.WriteLine($"Listening {endpoint.BbsType.Name}:{endpoint.Port}");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                var endpointName = $"{endpoint.BbsType.Name}:{endpoint.Port}";
                ServerMetrics.ConnectionAccepted(endpointName);

                var remote = (IPEndPoint?)client.Client.RemoteEndPoint;
                Console.WriteLine($"Accepted connection for {endpointName} from {remote?.Address}:{remote?.Port}");

                client.ReceiveTimeout = (int)timeout.TotalMilliseconds;
                client.SendTimeout = (int)timeout.TotalMilliseconds;

                var sessionId = sessionTracker.NextId();
                var sessionTask = RunClientSessionAsync(endpoint, endpointName, client, timeout, services, cancellationToken);
                sessionTracker.Add(sessionId, sessionTask);
                _ = sessionTask.ContinueWith(
                    _ => sessionTracker.Remove(sessionId),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task RunClientSessionAsync(
        EndPointSpec endpoint,
        string endpointName,
        TcpClient client,
        TimeSpan timeout,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        using (client)
        {
            var startedAt = DateTimeOffset.UtcNow;
            ServerMetrics.SessionStarted(endpointName, endpoint.BbsType.Name);

            try
            {
                var instance = (BbsThread)ActivatorUtilities.CreateInstance(services, endpoint.BbsType);
                var remote = (IPEndPoint?)client.Client.RemoteEndPoint;
                if (remote is not null)
                {
                    var location = await services.GetRequiredService<IClientGeolocationService>()
                        .LocateAsync(remote.Address, cancellationToken).ConfigureAwait(false);
                    if (location is not null) instance.SetCustomObject(ClientLocation.SessionKey, location);
                }
                Console.WriteLine($"Session started: {endpointName}, clientId=pending");
                await instance.RunSessionAsync(client, timeout, cancellationToken).ConfigureAwait(false);
                var duration = DateTimeOffset.UtcNow - startedAt;
                ServerMetrics.SessionCompleted(endpointName, endpoint.BbsType.Name, duration);
                Console.WriteLine($"Session ended: {endpointName}, clientId={instance.ClientId}, name={instance.ClientName}, duration={duration:c}");
            }
            catch (Exception ex)
            {
                var duration = DateTimeOffset.UtcNow - startedAt;
                ServerMetrics.SessionFailed(endpointName, endpoint.BbsType.Name, duration);
                Console.Error.WriteLine($"Session failed: {endpointName}, duration={duration:c}, error={ex.Message}");
            }
        }
    }

    private static async Task RunServicePortAsync(int servicePort, SessionTracker sessionTracker, CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Any, servicePort);
        listener.Start();
        Console.WriteLine($"Diagnostic endpoint listening on port {servicePort}");
        Console.WriteLine($"OpenTelemetry-compatible metrics endpoint: http://0.0.0.0:{servicePort}/metrics");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                var requestId = sessionTracker.NextId();
                var requestTask = RunServiceRequestAsync(client, cancellationToken);
                sessionTracker.Add(requestId, requestTask);
                _ = requestTask.ContinueWith(
                    _ => sessionTracker.Remove(requestId),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task RunServiceRequestAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            try
            {
                await using var stream = client.GetStream();
                var payload = await BuildServiceResponseAsync(stream, cancellationToken).ConfigureAwait(false);
                var body = Encoding.Latin1.GetBytes(payload);
                await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // normal shutdown
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Diagnostic request failed: {exception.Message}");
            }
        }
    }

    private static async Task<string> BuildServiceResponseAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[2048];
        var bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (bytesRead <= 0)
        {
            ServerMetrics.DiagnosticRequest();
            return BbsThread.BuildDiagnosticsHtml();
        }

        var request = Encoding.ASCII.GetString(buffer, 0, bytesRead);
        var path = ParseHttpPath(request);
        if (path.Equals("/metrics", StringComparison.OrdinalIgnoreCase))
        {
            ServerMetrics.MetricsRequest();
            return BuildMetricsHttpResponse();
        }

        ServerMetrics.DiagnosticRequest();
        return BbsThread.BuildDiagnosticsHtml();
    }

    private static string BuildMetricsHttpResponse()
    {
        var metrics = ServerMetrics.BuildOpenMetricsPayload();
        var sb = new StringBuilder();
        sb.AppendLine("HTTP/1.1 200 OK");
        sb.AppendLine("Server: CsharpBbs");
        sb.AppendLine("Content-Type: text/plain; version=0.0.4; charset=utf-8");
        sb.AppendLine("Connection: Closed");
        sb.AppendLine();
        sb.Append(metrics);
        return sb.ToString();
    }

    private static string ParseHttpPath(string request)
    {
        var firstLineEnd = request.IndexOf("\r\n", StringComparison.Ordinal);
        if (firstLineEnd < 0)
        {
            firstLineEnd = request.IndexOf('\n');
        }

        var firstLine = firstLineEnd > 0 ? request[..firstLineEnd] : request;
        var parts = firstLine.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return "/";
        }

        return parts[1];
    }

    private static bool TryParseArguments(
        IReadOnlyList<string> args,
        out List<EndPointSpec> endPoints,
        out TimeSpan timeout,
        out int servicePort,
        out bool showHelp,
        out List<string> errors)
    {
        endPoints = new List<EndPointSpec>();
        timeout = TimeSpan.FromHours(1);
        servicePort = 0;
        showHelp = false;
        errors = new List<string>();

        var usedPorts = new HashSet<int>();

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "-h":
                case "--help":
                    showHelp = true;
                    break;

                case "-t":
                case "--timeout":
                    if (!TryReadValue(args, ref i, out var timeoutValue))
                    {
                        errors.Add("Missing value for timeout.");
                        return false;
                    }

                    timeout = ParseTimeout(timeoutValue);
                    break;

                case "-s":
                case "--serviceport":
                    if (!TryReadValue(args, ref i, out var serviceValue) || !int.TryParse(serviceValue, out servicePort))
                    {
                        errors.Add("Invalid service port.");
                        return false;
                    }

                    break;

                case "--bbs":
                    while (i + 1 < args.Count && !args[i + 1].StartsWith("-", StringComparison.Ordinal))
                    {
                        i++;
                        var token = args[i];
                        var split = token.Split(':', 2);
                        if (split.Length != 2 || !int.TryParse(split[1], out var port))
                        {
                            errors.Add($"Invalid BBS endpoint '{token}'. Use Name:Port.");
                            return false;
                        }

                        if (!TenantMap.TryGetValue(split[0].ToLowerInvariant(), out var tenantType))
                        {
                            errors.Add($"Unknown BBS type '{split[0]}'.");
                            return false;
                        }

                        if (!usedPorts.Add(port))
                        {
                            errors.Add($"Port {port} already used.");
                            return false;
                        }

                        endPoints.Add(new EndPointSpec(tenantType, port));
                    }

                    break;

                default:
                    errors.Add($"Unknown option '{arg}'.");
                    return false;
            }
        }

        if (usedPorts.Contains(servicePort) && servicePort != 0)
        {
            errors.Add($"Declared service port {servicePort} is already used by a BBS endpoint.");
            return false;
        }

        return true;
    }

    private static bool TryReadValue(IReadOnlyList<string> args, ref int index, out string value)
    {
        if (index + 1 >= args.Count)
        {
            value = string.Empty;
            return false;
        }

        index++;
        value = args[index];
        return true;
    }

    private static TimeSpan ParseTimeout(string input)
    {
        var value = input.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return TimeSpan.FromHours(1);
        }

        if (value.EndsWith("h", StringComparison.OrdinalIgnoreCase) && int.TryParse(value[..^1], out var h))
        {
            return TimeSpan.FromHours(h);
        }

        if (value.EndsWith("m", StringComparison.OrdinalIgnoreCase) && int.TryParse(value[..^1], out var m))
        {
            return TimeSpan.FromMinutes(m);
        }

        if (value.EndsWith("s", StringComparison.OrdinalIgnoreCase) && int.TryParse(value[..^1], out var s))
        {
            return TimeSpan.FromSeconds(s);
        }

        return int.TryParse(value, out var ms) ? TimeSpan.FromMilliseconds(ms) : TimeSpan.FromHours(1);
    }

    private static Dictionary<string, Type> DiscoverTenants()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly =>
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    return ex.Types.Where(t => t is not null).Select(t => t!);
                }
            })
            .Where(type => type is not null)
            .Where(type => type is { IsAbstract: false })
            .Where(type => typeof(BbsThread).IsAssignableFrom(type))
            .Where(type => type.GetCustomAttribute<HiddenAttribute>() is null)
            .ToDictionary(type => type.Name.ToLowerInvariant(), type => type, StringComparer.OrdinalIgnoreCase);
    }
    private static void DisplayHelp()
    {
        Console.WriteLine("usage: bbs-server --bbs <bbsName:port> [<bbsName:port> ...] [-t <timeout>] [-s <servicePort>]");
        Console.WriteLine("  --bbs         Run specific BBS tenants in form <name>:<port>");
        Console.WriteLine("  -t --timeout  Socket timeout in ms, or with suffix h/m/s (default: 3600000)");
        Console.WriteLine("  -s --serviceport  TCP diagnostic port (default: 0 = disabled)");
        Console.WriteLine("  -h --help     Show help");
        Console.WriteLine();
        Console.WriteLine("Available BBS tenants:");
        foreach (var tenant in TenantMap.Values.Select(x => x.Name).OrderBy(x => x))
        {
            Console.WriteLine($" * {tenant}");
        }
    }
}









