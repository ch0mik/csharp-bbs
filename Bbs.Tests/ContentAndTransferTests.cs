using System.Net.Sockets;
using System.IO.Compression;
using Bbs.Core.Content;
using Bbs.Core.Protocols;
using Bbs.Terminals;

namespace Bbs.Tests;

public class ContentAndTransferTests
{
    [Fact]
    public void C64DownloadPayloadNormalizer_ShouldExtractPreferredEntryFromZip()
    {
        var zipBytes = BuildZip(
            ("readme.txt", "hello"u8.ToArray()),
            ("demo.prg", new byte[] { 1, 2, 3, 4 }));

        var payload = C64DownloadPayloadNormalizer.Normalize("archive.zip", zipBytes, "https://example.test/file");

        Assert.Equal("demo.prg", payload.FileName);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, payload.Content);
    }

    [Fact]
    public void C64DownloadPayloadNormalizer_ShouldGunzipPayload()
    {
        var original = new byte[] { 10, 20, 30, 40, 50 };
        var gzBytes = BuildGzip(original);

        var payload = C64DownloadPayloadNormalizer.Normalize("demo.prg.gz", gzBytes, "https://example.test/file");

        Assert.Equal("demo.prg", payload.FileName);
        Assert.Equal(original, payload.Content);
    }

    [Fact]
    public async Task XModemSender_ShouldSendBlocksAndFinishOnAck()
    {
        var (server, client) = await TestSocketPair.CreateAsync().ConfigureAwait(false);
        try
        {
            var io = new PetsciiInputOutput(server);
            var sender = new XModemSender();
            var payload = Enumerable.Range(0, 200).Select(i => (byte)i).ToArray();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var receiverTask = RunSimpleXModemReceiverAsync(client, expectedBlocks: 2, cts.Token);

            var result = await sender.SendAsync(io, payload, "demo.prg", cts.Token).ConfigureAwait(false);
            await receiverTask.ConfigureAwait(false);

            Assert.True(result.Success);
            Assert.Equal(2, result.BlocksSent);
            Assert.Equal(200, result.BytesSent);
        }
        finally
        {
            client.Dispose();
            server.Dispose();
        }
    }

    private static async Task RunSimpleXModemReceiverAsync(TcpClient client, int expectedBlocks, CancellationToken cancellationToken)
    {
        const byte soh = 0x01;
        const byte eot = 0x04;
        const byte ack = 0x06;
        const byte crcRequest = 0x43;

        var stream = client.GetStream();
        await stream.WriteAsync(new[] { crcRequest }, cancellationToken).ConfigureAwait(false);

        for (var i = 0; i < expectedBlocks; i++)
        {
            var packet = await ReadExactlyAsync(stream, 132, cancellationToken).ConfigureAwait(false);
            Assert.Equal(soh, packet[0]);
            await stream.WriteAsync(new[] { ack }, cancellationToken).ConfigureAwait(false);
        }

        var eotByte = await ReadExactlyAsync(stream, 1, cancellationToken).ConfigureAwait(false);
        Assert.Equal(eot, eotByte[0]);
        await stream.WriteAsync(new[] { ack }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadExactlyAsync(NetworkStream stream, int count, CancellationToken cancellationToken)
    {
        var buffer = new byte[count];
        var offset = 0;

        while (offset < count)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), cancellationToken).ConfigureAwait(false);
            if (read <= 0)
            {
                throw new IOException("Unexpected EOF in XMODEM test receiver.");
            }

            offset += read;
        }

        return buffer;
    }

    private static byte[] BuildZip(params (string Name, byte[] Content)[] entries)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in entries)
            {
                var zipEntry = zip.CreateEntry(entry.Name);
                using var stream = zipEntry.Open();
                stream.Write(entry.Content, 0, entry.Content.Length);
            }
        }

        return ms.ToArray();
    }

    private static byte[] BuildGzip(byte[] source)
    {
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionMode.Compress, leaveOpen: true))
        {
            gz.Write(source, 0, source.Length);
        }

        return ms.ToArray();
    }
}


