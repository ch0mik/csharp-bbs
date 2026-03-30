using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Bbs.Core;

public abstract class BbsThread
{
    private sealed record BbsStatus(
        bool KeepAlive,
        TimeSpan KeepAliveInterval,
        TimeSpan KeepAliveTimeout,
        int KeepAliveChar,
        BbsInputOutput Io,
        Type ClientClass,
        BbsThread? Child);

    private static long _clientCounter;

    private readonly ConcurrentStack<BbsStatus> BbsStack = new();

    protected readonly ConcurrentDictionary<string, object> CustomObject = new(StringComparer.OrdinalIgnoreCase);

    protected DateTimeOffset LastActivityAt = DateTimeOffset.UtcNow;

    private CancellationTokenSource? _sessionCts;
    private Task? _keepAliveTask;

    public static ConcurrentDictionary<long, BbsThread> Clients { get; } = new();

    public long ClientId { get; protected set; }

    public string ClientName { get; protected set; } = string.Empty;

    public Type ClientClass { get; protected set; } = typeof(BbsThread);

    public DateTimeOffset StartTimestamp { get; protected set; }

    public IPAddress? IpAddress { get; protected set; }

    public IPAddress? ServerAddress { get; protected set; }

    public int ServerPort { get; protected set; }

    public TcpClient? Client { get; protected set; }

    public BbsInputOutput Io { get; protected set; } = default!;

    public BbsThread? Parent { get; protected set; }

    public BbsThread? Child { get; protected set; }

    public bool KeepAlive { get; protected set; } = true;

    public TimeSpan KeepAliveTimeout { get; set; } = TimeSpan.FromHours(1);

    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromMinutes(1);

    public int KeepAliveChar { get; set; } = 0;

    public bool? LocalEcho { get; protected set; }

    public virtual string GetTerminalType() => "petscii";

    public virtual Task InitBbsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public abstract Task DoLoopAsync(CancellationToken cancellationToken = default);

    public abstract BbsInputOutput BuildIO(TcpClient client);

    public virtual byte[]? InitializingBytes() => null;

    public abstract int GetScreenColumns();

    public abstract int GetScreenRows();

    public abstract void Cls();

    public virtual async Task RunSessionAsync(TcpClient client, TimeSpan defaultTimeout, CancellationToken cancellationToken = default)
    {
        _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        StartTimestamp = DateTimeOffset.UtcNow;
        LastActivityAt = StartTimestamp;

        ClientId = Interlocked.Increment(ref _clientCounter);
        ClientClass = GetType();
        ClientName = $"client{ClientId}";
        Client = client;
        IpAddress = ((IPEndPoint)client.Client.RemoteEndPoint!).Address;
        ServerAddress = ((IPEndPoint)client.Client.LocalEndPoint!).Address;
        ServerPort = ((IPEndPoint)client.Client.LocalEndPoint!).Port;

        if (KeepAliveTimeout <= TimeSpan.Zero)
        {
            KeepAliveTimeout = defaultTimeout;
        }

        Io = BuildIO(client);
        if (LocalEcho.HasValue)
        {
            Io.LocalEcho = LocalEcho.Value;
        }

        Clients[ClientId] = this;
        SessionLifecycleHooks.RaiseSessionStarted(this);

        try
        {
            var qMode = Io.QuoteMode;
            var init = InitializingBytes();
            if (init is { Length: > 0 })
            {
                Io.Write(init);
                await Io.FlushAsync(_sessionCts.Token).ConfigureAwait(false);
                await Io.ResetInputAsync(_sessionCts.Token).ConfigureAwait(false);
            }

            await InitBbsAsync(_sessionCts.Token).ConfigureAwait(false);
            Io.SetQuoteMode(qMode);

            RestartKeepAlive();
            _keepAliveTask = Task.Run(() => KeepAliveLoopAsync(_sessionCts.Token), _sessionCts.Token);

            await DoLoopAsync(_sessionCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // normal shutdown path
        }
        catch (IOException)
        {
            // EOF / socket closure
        }
        finally
        {
            try
            {
                _sessionCts.Cancel();
            }
            catch
            {
                // ignored
            }

            try
            {
                Io?.Shutdown();
            }
            catch
            {
                // ignored
            }

            SessionLifecycleHooks.RaiseSessionEnded(this);
            Clients.TryRemove(ClientId, out _);
        }
    }

    private async Task KeepAliveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(KeepAliveInterval, cancellationToken).ConfigureAwait(false);

            if (!KeepAlive || Io.QuoteMode)
            {
                continue;
            }

            var idleFor = DateTimeOffset.UtcNow - LastActivityAt;
            if (idleFor > KeepAliveTimeout)
            {
                _sessionCts?.Cancel();
                return;
            }

            try
            {
                if (KeepAliveChar > 0)
                {
                    Io.Write((byte)KeepAliveChar);
                    await Io.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch
            {
                _sessionCts?.Cancel();
                return;
            }
        }
    }

    public void RestartKeepAlive()
    {
        LastActivityAt = DateTimeOffset.UtcNow;
    }

    public void SetLocalEcho(bool value)
    {
        LocalEcho = value;
        if (Io is not null)
        {
            Io.LocalEcho = value;
        }
    }

    public bool GetLocalEcho() => Io?.LocalEcho ?? LocalEcho ?? true;

    public BbsThread GetRoot()
    {
        var root = this;
        while (root.Parent is not null)
        {
            root = root.Parent;
        }

        return root;
    }

    public object? GetCustomObject(string key) => GetRoot().CustomObject.TryGetValue(key, out var obj) ? obj : null;

    public void SetCustomObject(string key, object value) => GetRoot().CustomObject[key] = value;

    public int ChangeClientName(string newName)
    {
        var candidate = (newName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return -1;
        }

        var conflict = Clients.Values.Any(c => c.ClientId != ClientId && string.Equals(c.ClientName, candidate, StringComparison.OrdinalIgnoreCase));
        if (conflict)
        {
            return -2;
        }

        ClientName = candidate;
        return 0;
    }

    public virtual void Receive(long senderId, object message)
    {
        Child?.Receive(senderId, message);
    }

    public int Send(long receiverId, object message)
    {
        if (!Clients.TryGetValue(receiverId, out var receiver))
        {
            return 1;
        }

        receiver.Receive(ClientId, message);
        return 0;
    }

    public async Task<bool> LaunchAsync(BbsThread bbs, CancellationToken cancellationToken = default)
    {
        var root = GetRoot();
        if (root.Client is null)
        {
            return false;
        }

        var childIo = bbs.BuildIO(root.Client);
        if (bbs.LocalEcho is null)
        {
            bbs.LocalEcho = GetLocalEcho();
        }

        if (bbs.LocalEcho.HasValue)
        {
            childIo.LocalEcho = bbs.LocalEcho.Value;
        }

        bbs.Parent = this;
        bbs.Client = root.Client;
        bbs.Io = childIo;
        bbs.ClientId = root.ClientId;
        bbs.ClientName = root.ClientName;
        bbs.ClientClass = bbs.GetType();
        bbs.IpAddress = root.IpAddress;
        bbs.ServerAddress = root.ServerAddress;
        bbs.ServerPort = root.ServerPort;
        bbs.KeepAlive = bbs.KeepAlive;
        bbs.KeepAliveTimeout = bbs.KeepAliveTimeout <= TimeSpan.Zero ? root.KeepAliveTimeout : bbs.KeepAliveTimeout;

        root.BbsStack.Push(new BbsStatus(
            root.KeepAlive,
            root.KeepAliveInterval,
            root.KeepAliveTimeout,
            root.KeepAliveChar,
            root.Io,
            root.ClientClass,
            root.Child));

        root.Io = childIo;
        root.Child = bbs;
        root.ClientClass = bbs.GetType();
        root.KeepAlive = bbs.KeepAlive;
        root.KeepAliveInterval = bbs.KeepAliveInterval;
        root.KeepAliveTimeout = bbs.KeepAliveTimeout;
        root.KeepAliveChar = bbs.KeepAliveChar;
        root.RestartKeepAlive();

        try
        {
            System.Console.WriteLine($"[LaunchAsync {bbs.GetType().Name}] Calling InitBbsAsync");
            var qMode = bbs.Io.QuoteMode;
            var init = bbs.InitializingBytes();
            if (init is { Length: > 0 })
            {
                bbs.Io.Write(init);
                await bbs.Io.FlushAsync(cancellationToken).ConfigureAwait(false);
                await bbs.Io.ResetInputAsync(cancellationToken).ConfigureAwait(false);
            }

            await bbs.InitBbsAsync(cancellationToken).ConfigureAwait(false);
            System.Console.WriteLine($"[LaunchAsync {bbs.GetType().Name}] InitBbsAsync completed, calling DoLoopAsync");
            bbs.Io.SetQuoteMode(qMode);
            await bbs.DoLoopAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            if (root.BbsStack.TryPop(out var status))
            {
                root.KeepAlive = status.KeepAlive;
                root.KeepAliveInterval = status.KeepAliveInterval;
                root.KeepAliveTimeout = status.KeepAliveTimeout;
                root.KeepAliveChar = status.KeepAliveChar;
                root.Io = status.Io;
                root.ClientClass = status.ClientClass;
                root.Child = status.Child;
                root.RestartKeepAlive();
            }
        }
    }

    public void Print(string? message) => Io.Print(NormalizeOutput(message));

    public void Println(string? message = null) => Io.Println(NormalizeOutput(message));

    public void PrintRaw(string? message) => Io.PrintRaw(message);

    public void PrintlnRaw(string? message) => Io.PrintlnRaw(message);
    protected virtual string NormalizeOutput(string? value)
    {
        var text = value ?? string.Empty;

        // PETSCII terminals cannot display Polish diacritics reliably.
        if (GetTerminalType().Equals("petscii", StringComparison.OrdinalIgnoreCase))
        {
            return TransliterateForPetscii(text);
        }

        return text;
    }

    private static string TransliterateForPetscii(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // Step 1: explicit mappings for letters that do not decompose well.
        var mapped = new StringBuilder(value.Length * 2);
        foreach (var c in value)
        {
            switch (c)
            {
                case 'ß': mapped.Append("ss"); break;
                case 'Æ': mapped.Append("AE"); break;
                case 'æ': mapped.Append("ae"); break;
                case 'Œ': mapped.Append("OE"); break;
                case 'œ': mapped.Append("oe"); break;
                case 'Ø': mapped.Append('O'); break;
                case 'ø': mapped.Append('o'); break;
                case 'Ł': mapped.Append('L'); break;
                case 'ł': mapped.Append('l'); break;
                case 'Đ': mapped.Append('D'); break;
                case 'đ': mapped.Append('d'); break;
                case 'Þ': mapped.Append("Th"); break;
                case 'þ': mapped.Append("th"); break;
                case 'Ð': mapped.Append('D'); break;
                case 'ð': mapped.Append('d'); break;
                case 'Ħ': mapped.Append('H'); break;
                case 'ħ': mapped.Append('h'); break;
                case 'ı': mapped.Append('i'); break;
                case 'Ŋ': mapped.Append('N'); break;
                case 'ŋ': mapped.Append('n'); break;
                case '‘':
                case '’':
                case '‚':
                    mapped.Append('\'');
                    break;
                case '“':
                case '”':
                case '„':
                    mapped.Append('"');
                    break;
                case '–':
                case '—':
                case '−':
                    mapped.Append('-');
                    break;
                case '…':
                    mapped.Append("...");
                    break;
                case '\u00A0':
                    mapped.Append(' ');
                    break;
                default:
                    mapped.Append(c);
                    break;
            }
        }

        // Step 2: remove combining diacritical marks.
        var decomposed = mapped.ToString().Normalize(NormalizationForm.FormD);
        var filtered = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category is UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            filtered.Append(c);
        }

        return filtered.ToString().Normalize(NormalizationForm.FormC);
    }

    public void Write(params int[] bytes) => Io.Write(bytes);

    public void Write(byte[] bytes) => Io.Write(bytes);

    public async Task FlushAsync(CancellationToken cancellationToken = default) => await Io.FlushAsync(cancellationToken).ConfigureAwait(false);

    public async Task<string> ReadLineAsync(int maxLength = 0, CancellationToken cancellationToken = default)
    {
        RestartKeepAlive();
        var result = await Io.ReadLineAsync(maxLength: maxLength, cancellationToken: cancellationToken).ConfigureAwait(false);
        RestartKeepAlive();
        return result;
    }

    public async Task<string> ReadLineAsync(ISet<int> allowedChars, CancellationToken cancellationToken = default)
    {
        RestartKeepAlive();
        var result = await Io.ReadLineAsync(allowedChars: allowedChars, cancellationToken: cancellationToken).ConfigureAwait(false);
        RestartKeepAlive();
        return result;
    }

    public async Task<string> ReadPasswordAsync(CancellationToken cancellationToken = default)
    {
        RestartKeepAlive();
        var result = await Io.ReadPasswordAsync(cancellationToken).ConfigureAwait(false);
        RestartKeepAlive();
        return result;
    }

    public async Task<int> ReadKeyAsync(CancellationToken cancellationToken = default)
    {
        RestartKeepAlive();
        var result = await Io.ReadKeyAsync(cancellationToken).ConfigureAwait(false);
        RestartKeepAlive();
        return result;
    }

    public async Task<int> KeyPressedAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        RestartKeepAlive();
        var result = await Io.KeyPressedAsync(timeout, cancellationToken).ConfigureAwait(false);
        RestartKeepAlive();
        return result;
    }

    public async Task<byte[]> ResetInputAsync(CancellationToken cancellationToken = default)
        => await Io.ResetInputAsync(cancellationToken).ConfigureAwait(false);

    public static IReadOnlyList<ClientSnapshot> GetClientSnapshots()
        => Clients.Values
            .OrderBy(c => c.ClientId)
            .Select(c => new ClientSnapshot(
                c.ClientId,
                c.ClientName,
                c.ClientClass.Name,
                c.IpAddress?.ToString() ?? "n/a",
                c.ServerPort,
                c.StartTimestamp,
                c.LastActivityAt))
            .ToList();

    public static string BuildDiagnosticsHtml()
    {
        var sb = new StringBuilder();
        var clients = GetClientSnapshots();

        sb.AppendLine("HTTP/1.1 200 OK");
        sb.AppendLine("Server: CsharpBbs");
        sb.AppendLine("Content-Type: text/html; charset=ISO-8859-1");
        sb.AppendLine("Connection: Closed");
        sb.AppendLine();
        sb.AppendLine("<html><head><title>BBS Diagnostics</title></head><body><pre>");
        sb.AppendLine($"Number of clients: {clients.Count}");
        sb.AppendLine();

        foreach (var client in clients)
        {
            var uptime = DateTimeOffset.UtcNow - client.StartedAt;
            var idle = DateTimeOffset.UtcNow - client.LastActivityAt;
            sb.AppendLine($"#{client.ClientId} {client.ClientClass}:{client.ServerPort} (uptime={uptime:c}, idle={idle:c}, name={client.ClientName}, ip={client.ClientIp})");
        }

        sb.AppendLine("</pre></body></html>");
        return sb.ToString();
    }
}










