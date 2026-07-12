using System.Net.Sockets;
using System.Text;

namespace Bbs.Tenants.Content.Games;

internal static class GameHighScores
{
    private static readonly RedisStore? Redis = RedisStore.CreateFromEnvironment();
    public static bool IsAvailable => Redis is not null;

    public static int Submit(string game, string player, int score)
    {
        game = Normalize(game, "game");
        player = Normalize(player, "anonymous");
        if (Redis is null) return 0;
        Redis?.Submit(game, player, score);
        return Best(game);
    }

    public static int Best(string game)
    {
        game = Normalize(game, "game");
        return Redis?.Best(game) ?? 0;
    }

    public static bool IsNewHighScore(string game, int score)
        => IsAvailable && score > Best(game);

    private static string Normalize(string value, string fallback)
    {
        var clean = new string((value ?? "").Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').Take(32).ToArray());
        return string.IsNullOrWhiteSpace(clean) ? fallback : clean.ToLowerInvariant();
    }

    private sealed class RedisStore(string host, int port, string? password)
    {
        public static RedisStore? CreateFromEnvironment()
        {
            var host = Environment.GetEnvironmentVariable("REDIS_HOST")?.Trim();
            if (string.IsNullOrWhiteSpace(host)) return null;
            var port = int.TryParse(Environment.GetEnvironmentVariable("REDIS_PORT"), out var parsed) ? parsed : 6379;
            return new(host, port, Environment.GetEnvironmentVariable("REDIS_PASSWORD")?.Trim());
        }

        public void Submit(string game, string player, int score)
        {
            try { using var stream = Connect(); Send(stream, "ZADD", Key(game), "GT", score.ToString(), player); _ = ReadReply(stream); }
            catch (Exception ex) { DebugLog($"submit failed: {ex.Message}"); }
        }

        public int? Best(string game)
        {
            try
            {
                using var stream = Connect(); Send(stream, "ZREVRANGE", Key(game), "0", "0", "WITHSCORES");
                var reply = ReadReply(stream) as List<object?>;
                return reply is { Count: >= 2 } && int.TryParse(reply[1]?.ToString(), out var score) ? score : 0;
            }
            catch (Exception ex) { DebugLog($"read failed: {ex.Message}"); return null; }
        }

        private NetworkStream Connect()
        {
            var client = new TcpClient(); client.Connect(host, port); var stream = client.GetStream();
            if (!string.IsNullOrEmpty(password)) { Send(stream, "AUTH", password); _ = ReadReply(stream); }
            return stream;
        }
        private static string Key(string game) => $"bbs:highscore:{game}";
        private static void Send(Stream stream, params string[] parts)
        {
            var text = $"*{parts.Length}\r\n" + string.Concat(parts.Select(p => $"${Encoding.UTF8.GetByteCount(p)}\r\n{p}\r\n"));
            var bytes = Encoding.UTF8.GetBytes(text); stream.Write(bytes); stream.Flush();
        }
        private static object? ReadReply(Stream stream)
        {
            var prefix = stream.ReadByte();
            return prefix switch
            {
                '+' => ReadLine(stream), ':' => ReadLine(stream), '-' => throw new IOException(ReadLine(stream)),
                '$' => ReadBulk(stream), '*' => ReadArray(stream), _ => throw new IOException("Invalid Redis reply")
            };
        }
        private static string? ReadBulk(Stream stream)
        {
            var length = int.Parse(ReadLine(stream)); if (length < 0) return null;
            var bytes = new byte[length]; stream.ReadExactly(bytes); _ = stream.ReadByte(); _ = stream.ReadByte(); return Encoding.UTF8.GetString(bytes);
        }
        private static List<object?> ReadArray(Stream stream)
        {
            var count = int.Parse(ReadLine(stream)); var result = new List<object?>(Math.Max(0, count));
            for (var i = 0; i < count; i++) result.Add(ReadReply(stream)); return result;
        }
        private static string ReadLine(Stream stream)
        {
            using var ms = new MemoryStream();
            while (true) { var b = stream.ReadByte(); if (b < 0) throw new EndOfStreamException(); if (b == '\r') { _ = stream.ReadByte(); break; } ms.WriteByte((byte)b); }
            return Encoding.UTF8.GetString(ms.ToArray());
        }
        private static void DebugLog(string message)
        {
            if (string.Equals(Environment.GetEnvironmentVariable("BBS_DEBUG"), "true", StringComparison.OrdinalIgnoreCase))
                Console.WriteLine($"[{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}][HighScores] {message}");
        }
    }
}
