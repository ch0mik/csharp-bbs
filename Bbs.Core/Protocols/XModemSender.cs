namespace Bbs.Core.Protocols;

public sealed record XModemSendResult(
    bool Success,
    int BlocksSent,
    int BytesSent,
    string Message);

public interface IXModemSender
{
    Task<XModemSendResult> SendAsync(
        BbsInputOutput io,
        byte[] payload,
        string fileName,
        CancellationToken cancellationToken = default);
}

public sealed class XModemSender : IXModemSender
{
    private const byte Soh = 0x01;
    private const byte Eot = 0x04;
    private const byte Ack = 0x06;
    private const byte Nak = 0x15;
    private const byte Can = 0x18;
    private const byte CrcRequest = 0x43; // 'C'
    private const byte Sub = 0x1A;

    private const int BlockSize = 128;
    private const int ReceiverReadyTimeoutSeconds = 30;
    private const int BlockAckTimeoutSeconds = 10;
    private const int MaxRetriesPerBlock = 10;

    public async Task<XModemSendResult> SendAsync(
        BbsInputOutput io,
        byte[] payload,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (io is null)
        {
            throw new ArgumentNullException(nameof(io));
        }

        if (payload is null || payload.Length == 0)
        {
            return new XModemSendResult(false, 0, 0, "No data to send.");
        }

        var receiverReady = await WaitReceiverReadyAsync(io, cancellationToken).ConfigureAwait(false);
        if (!receiverReady)
        {
            return new XModemSendResult(false, 0, 0, "Receiver did not start XMODEM (missing NAK/C).");
        }

        var blockNumber = 1;
        var sentBlocks = 0;
        var offset = 0;
        while (offset < payload.Length)
        {
            var packet = BuildPacket(payload, offset, blockNumber);
            var sent = false;

            for (var attempt = 1; attempt <= MaxRetriesPerBlock; attempt++)
            {
                io.Write(packet);
                await io.FlushAsync(cancellationToken).ConfigureAwait(false);

                var answer = await io.KeyPressedAsync(TimeSpan.FromSeconds(BlockAckTimeoutSeconds), cancellationToken).ConfigureAwait(false);
                if (answer == Ack)
                {
                    sent = true;
                    break;
                }

                if (answer == Can)
                {
                    return new XModemSendResult(false, sentBlocks, Math.Min(offset, payload.Length), "Transfer canceled by receiver.");
                }

                if (answer != Nak && answer != CrcRequest)
                {
                    // Unknown/no response; retry as if NAK.
                }
            }

            if (!sent)
            {
                return new XModemSendResult(false, sentBlocks, Math.Min(offset, payload.Length), "Block retry limit reached.");
            }

            sentBlocks++;
            offset += BlockSize;
            blockNumber = (blockNumber + 1) & 0xFF;
            if (blockNumber == 0)
            {
                blockNumber = 1;
            }
        }

        for (var attempt = 0; attempt < MaxRetriesPerBlock; attempt++)
        {
            io.Write(Eot);
            await io.FlushAsync(cancellationToken).ConfigureAwait(false);

            var answer = await io.KeyPressedAsync(TimeSpan.FromSeconds(BlockAckTimeoutSeconds), cancellationToken).ConfigureAwait(false);
            if (answer == Ack)
            {
                return new XModemSendResult(true, sentBlocks, payload.Length, $"Sent '{fileName}' ({payload.Length} bytes).");
            }

            if (answer == Can)
            {
                return new XModemSendResult(false, sentBlocks, payload.Length, "Transfer canceled by receiver.");
            }
        }

        return new XModemSendResult(false, sentBlocks, payload.Length, "EOT was not acknowledged.");
    }

    private static async Task<bool> WaitReceiverReadyAsync(BbsInputOutput io, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(ReceiverReadyTimeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var key = await io.KeyPressedAsync(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            if (key == Nak || key == CrcRequest)
            {
                return true;
            }

            if (key == Can)
            {
                return false;
            }
        }

        return false;
    }

    private static byte[] BuildPacket(byte[] payload, int offset, int blockNumber)
    {
        var data = new byte[BlockSize];
        var remaining = Math.Max(0, payload.Length - offset);
        var copy = Math.Min(remaining, BlockSize);
        if (copy > 0)
        {
            Array.Copy(payload, offset, data, 0, copy);
        }

        if (copy < BlockSize)
        {
            Array.Fill(data, Sub, copy, BlockSize - copy);
        }

        var checksum = ComputeChecksum(data);
        var packet = new byte[3 + BlockSize + 1];
        packet[0] = Soh;
        packet[1] = (byte)blockNumber;
        packet[2] = (byte)(255 - packet[1]);
        Array.Copy(data, 0, packet, 3, BlockSize);
        packet[packet.Length - 1] = checksum;
        return packet;
    }

    private static byte ComputeChecksum(byte[] data)
    {
        var sum = 0;
        foreach (var b in data)
        {
            sum = (sum + b) & 0xFF;
        }

        return (byte)sum;
    }
}
