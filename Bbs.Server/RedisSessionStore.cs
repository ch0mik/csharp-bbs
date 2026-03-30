using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Bbs.Core;

namespace Bbs.Server;

internal sealed class RedisSessionStore
{
    private const int ActiveTtlSeconds = 60 * 60 * 4;

    private readonly string _host;
    private readonly int _port;
    private readonly string _password;
    private readonly string _instanceId;

    private RedisSessionStore(string host, int port, string password, string instanceId)
    {
        _host = host;
        _port = port;
        _password = password;
        _instanceId = instanceId;
    }

    public static RedisSessionStore? CreateFromEnvironment()
    {
        var host = Environment.GetEnvironmentVariable("REDIS_HOST");
        var password = Environment.GetEnvironmentVariable("REDIS_PASSWORD");
        var portRaw = Environment.GetEnvironmentVariable("REDIS_PORT");
        var instanceId = Environment.GetEnvironmentVariable("BBS_INSTANCE_ID");

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var port = 6379;
        if (!string.IsNullOrWhiteSpace(portRaw) && int.TryParse(portRaw, out var parsed) && parsed > 0)
        {
            port = parsed;
        }

        if (string.IsNullOrWhiteSpace(instanceId))
        {
            instanceId = Environment.MachineName;
        }

        return new RedisSessionStore(host.Trim(), port, password.Trim(), instanceId.Trim());
    }

    public void UpsertActiveSession(BbsThread thread)
    {
        var key = BuildSessionKey(thread.ClientId);
        var payload = JsonSerializer.Serialize(new
        {
            instanceId = _instanceId,
            clientId = thread.ClientId,
            clientName = thread.ClientName,
            tenant = thread.ClientClass.Name,
            remoteIp = thread.IpAddress?.ToString(),
            serverPort = thread.ServerPort,
            startedAtUtc = thread.StartTimestamp.ToString("O")
        });

        ExecuteCommand("SET", key, payload, "EX", ActiveTtlSeconds.ToString());
    }

    public void RemoveActiveSession(long clientId)
    {
        ExecuteCommand("DEL", BuildSessionKey(clientId));
    }

    private string BuildSessionKey(long clientId)
    {
        return $"bbs:session:{_instanceId}:{clientId}";
    }

    private void ExecuteCommand(params string[] parts)
    {
        try
        {
            using var client = new TcpClient();
            client.Connect(_host, _port);
            using var stream = client.GetStream();

            if (!string.IsNullOrEmpty(_password))
            {
                SendCommand(stream, "AUTH", _password);
                ReadAndValidateReply(stream);
            }

            SendCommand(stream, parts);
            ReadAndValidateReply(stream);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Redis session store error: {ex.Message}");
        }
    }

    private static void SendCommand(NetworkStream stream, params string[] parts)
    {
        var sb = new StringBuilder();
        sb.Append('*').Append(parts.Length).Append("\r\n");
        foreach (var part in parts)
        {
            var p = part ?? string.Empty;
            var bytes = Encoding.UTF8.GetByteCount(p);
            sb.Append('$').Append(bytes).Append("\r\n").Append(p).Append("\r\n");
        }

        var raw = Encoding.UTF8.GetBytes(sb.ToString());
        stream.Write(raw, 0, raw.Length);
        stream.Flush();
    }

    private static void ReadAndValidateReply(NetworkStream stream)
    {
        var prefix = stream.ReadByte();
        if (prefix < 0)
        {
            throw new IOException("Redis closed connection.");
        }

        switch ((char)prefix)
        {
            case '+':
            case ':':
                ReadLine(stream);
                return;
            case '$':
            {
                var lenRaw = ReadLine(stream);
                if (!int.TryParse(lenRaw, out var len))
                {
                    throw new IOException($"Invalid Redis bulk length: {lenRaw}");
                }

                if (len >= 0)
                {
                    ReadBytes(stream, len + 2);
                }

                return;
            }
            case '-':
                throw new IOException("Redis error: " + ReadLine(stream));
            default:
                throw new IOException($"Unexpected Redis reply prefix: {(char)prefix}");
        }
    }

    private static string ReadLine(NetworkStream stream)
    {
        using var ms = new MemoryStream();
        while (true)
        {
            var b = stream.ReadByte();
            if (b < 0)
            {
                throw new IOException("Unexpected EOF while reading Redis response line.");
            }

            if (b == '\r')
            {
                var next = stream.ReadByte();
                if (next != '\n')
                {
                    throw new IOException("Malformed Redis response line ending.");
                }

                break;
            }

            ms.WriteByte((byte)b);
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void ReadBytes(NetworkStream stream, int count)
    {
        var buffer = new byte[1024];
        var remaining = count;
        while (remaining > 0)
        {
            var read = stream.Read(buffer, 0, Math.Min(buffer.Length, remaining));
            if (read <= 0)
            {
                throw new IOException("Unexpected EOF while reading Redis response payload.");
            }

            remaining -= read;
        }
    }
}
