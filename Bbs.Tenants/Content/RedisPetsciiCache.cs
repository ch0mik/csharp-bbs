using System.Net.Sockets;
using System.Text;

namespace Bbs.Tenants.Content;

internal sealed class RedisPetsciiCache
{
    private readonly string _host;
    private readonly int _port;
    private readonly string? _password;

    private RedisPetsciiCache(string host, int port, string? password)
    {
        _host = host;
        _port = port;
        _password = string.IsNullOrWhiteSpace(password) ? null : password.Trim();
    }

    public static RedisPetsciiCache? CreateFromEnvironment()
    {
        var host = Environment.GetEnvironmentVariable("REDIS_HOST")?.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var portRaw = Environment.GetEnvironmentVariable("REDIS_PORT");
        var port = 6379;
        if (!string.IsNullOrWhiteSpace(portRaw) && int.TryParse(portRaw, out var parsed) && parsed > 0)
        {
            port = parsed;
        }

        var password = Environment.GetEnvironmentVariable("REDIS_PASSWORD");
        return new RedisPetsciiCache(host, port, password);
    }

    public bool TryGet(string key, out byte[] value)
    {
        value = Array.Empty<byte>();
        try
        {
            using var client = new TcpClient();
            client.Connect(_host, _port);
            using var stream = client.GetStream();

            if (!string.IsNullOrEmpty(_password))
            {
                SendCommand(stream, "AUTH"u8.ToArray(), Encoding.UTF8.GetBytes(_password));
                ReadSimpleOkReply(stream);
            }

            SendCommand(stream, "GET"u8.ToArray(), Encoding.UTF8.GetBytes(key));
            value = ReadBulkReply(stream);
            if (value.Length > 0)
            {
                DebugLog($"GET hit: key='{key}', bytes={value.Length}");
                return true;
            }

            DebugLog($"GET miss: key='{key}'");
            return false;
        }
        catch (Exception ex)
        {
            DebugLog($"GET error: key='{key}', error='{ex.Message}'");
            return false;
        }
    }

    public void Set(string key, byte[] value, TimeSpan ttl)
    {
        if (value is null || value.Length == 0)
        {
            return;
        }

        var ttlSeconds = Math.Max(1, (int)ttl.TotalSeconds);

        try
        {
            using var client = new TcpClient();
            client.Connect(_host, _port);
            using var stream = client.GetStream();

            if (!string.IsNullOrEmpty(_password))
            {
                SendCommand(stream, "AUTH"u8.ToArray(), Encoding.UTF8.GetBytes(_password));
                ReadSimpleOkReply(stream);
            }

            SendCommand(
                stream,
                "SET"u8.ToArray(),
                Encoding.UTF8.GetBytes(key),
                value,
                "EX"u8.ToArray(),
                Encoding.UTF8.GetBytes(ttlSeconds.ToString()));
            ReadSimpleOkReply(stream);
            DebugLog($"SET ok: key='{key}', bytes={value.Length}, ttl_sec={ttlSeconds}");
        }
        catch (Exception ex)
        {
            DebugLog($"SET error: key='{key}', error='{ex.Message}'");
        }
    }

    private static void SendCommand(NetworkStream stream, params byte[][] parts)
    {
        using var ms = new MemoryStream();
        WriteAscii(ms, $"*{parts.Length}\r\n");
        foreach (var part in parts)
        {
            var data = part ?? Array.Empty<byte>();
            WriteAscii(ms, $"${data.Length}\r\n");
            ms.Write(data, 0, data.Length);
            WriteAscii(ms, "\r\n");
        }

        var payload = ms.ToArray();
        stream.Write(payload, 0, payload.Length);
        stream.Flush();
    }

    private static void WriteAscii(Stream stream, string text)
    {
        var data = Encoding.ASCII.GetBytes(text);
        stream.Write(data, 0, data.Length);
    }

    private static void ReadSimpleOkReply(NetworkStream stream)
    {
        var prefix = stream.ReadByte();
        if (prefix < 0)
        {
            throw new IOException("Redis closed connection.");
        }

        switch ((char)prefix)
        {
            case '+':
                _ = ReadLine(stream);
                return;
            case '-':
                throw new IOException("Redis error: " + ReadLine(stream));
            default:
                throw new IOException($"Unexpected Redis reply prefix: {(char)prefix}");
        }
    }

    private static byte[] ReadBulkReply(NetworkStream stream)
    {
        var prefix = stream.ReadByte();
        if (prefix < 0)
        {
            throw new IOException("Redis closed connection.");
        }

        if (prefix == '-')
        {
            throw new IOException("Redis error: " + ReadLine(stream));
        }

        if (prefix != '$')
        {
            throw new IOException($"Unexpected Redis bulk reply prefix: {(char)prefix}");
        }

        var lenRaw = ReadLine(stream);
        if (!int.TryParse(lenRaw, out var length))
        {
            throw new IOException($"Invalid Redis bulk length: {lenRaw}");
        }

        if (length <= 0)
        {
            if (length == 0)
            {
                ReadExact(stream, 2); // CRLF
            }

            return Array.Empty<byte>();
        }

        var payload = ReadExact(stream, length);
        ReadExact(stream, 2); // CRLF
        return payload;
    }

    private static byte[] ReadExact(NetworkStream stream, int count)
    {
        var buffer = new byte[count];
        var offset = 0;
        while (offset < count)
        {
            var read = stream.Read(buffer, offset, count - offset);
            if (read <= 0)
            {
                throw new IOException("Unexpected EOF while reading Redis response.");
            }

            offset += read;
        }

        return buffer;
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

    private static void DebugLog(string message)
    {
        Console.WriteLine($"[{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}][RedisPetsciiCache] {message}");
    }
}
