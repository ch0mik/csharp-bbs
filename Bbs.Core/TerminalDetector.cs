using System.Net.Sockets;

namespace Bbs.Core;

/// <summary>
/// Detects the terminal type by sending probe sequences and analyzing responses.
/// PETSCII (Commodore 64) clients typically ignore telnet control sequences.
/// ASCII/Telnet clients respond to or negotiate telnet options.
/// </summary>
public sealed class TerminalDetector
{
    private readonly NetworkStream _stream;
    private readonly int _timeoutMs;

    public TerminalDetector(NetworkStream stream, int timeoutMs = 1500)
    {
        _stream = stream;
        _timeoutMs = timeoutMs;
    }

    /// <summary>
    /// Detects whether the client is PETSCII (Commodore 64) or ASCII (Telnet).
    /// Returns TerminalType.Petscii or TerminalType.Ascii based on detection.
    /// </summary>
    public async Task<TerminalType> DetectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var telnetProbe = new byte[] { 255, 251, 1 };

            Console.WriteLine("[TerminalDetector] Sending IAC WILL ECHO probe (bytes: 255,251,1)");
            _stream.Write(telnetProbe, 0, telnetProbe.Length);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            _stream.ReadTimeout = _timeoutMs;

            var responseBuffer = new byte[128];
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeoutMs);

            try
            {
                int bytesRead = await _stream.ReadAsync(responseBuffer, 0, responseBuffer.Length, cts.Token)
                    .ConfigureAwait(false);

                var hexDump = string.Join(",", responseBuffer.Take(bytesRead).Select(b => b.ToString()));
                Console.WriteLine($"[TerminalDetector] Probe response: {bytesRead} bytes = [{hexDump}]");

                if (bytesRead > 0)
                {
                    var probeEcho = bytesRead == 3 &&
                                    responseBuffer[0] == 255 &&
                                    responseBuffer[1] == 251 &&
                                    responseBuffer[2] == 1;

                    if (probeEcho)
                    {
                        Console.WriteLine("[TerminalDetector] RESULT: Got exact echo of probe -> PETSCII");
                        return TerminalType.Petscii;
                    }

                    if (responseBuffer.Any(b => b == 255))
                    {
                        Console.WriteLine("[TerminalDetector] RESULT: Found non-echo IAC byte -> ASCII");
                        return TerminalType.Ascii;
                    }

                    if (responseBuffer.Take(bytesRead).Any(b => b >= 128))
                    {
                        Console.WriteLine("[TerminalDetector] RESULT: Found high-bit char (>=128) -> PETSCII");
                        return TerminalType.Petscii;
                    }

                    if (responseBuffer.Take(bytesRead).Any(b => b >= 32 && b <= 126))
                    {
                        Console.WriteLine("[TerminalDetector] RESULT: Found printable ASCII -> ASCII");
                        return TerminalType.Ascii;
                    }
                }

                Console.WriteLine("[TerminalDetector] RESULT: No response -> PETSCII (default)");
                return TerminalType.Petscii;
            }
            catch (OperationCanceledException)
            {
                return TerminalType.Petscii;
            }
            finally
            {
                cts.Dispose();
                _stream.ReadTimeout = Timeout.Infinite;
            }
        }
        catch
        {
            return TerminalType.Petscii;
        }
    }
}

/// <summary>
/// Terminal type detected from client capabilities.
/// </summary>
public enum TerminalType
{
    /// <summary>Commodore 64 PETSCII terminal (40x25 mode)</summary>
    Petscii,

    /// <summary>ASCII/Telnet legacy terminal (80x24 mode)</summary>
    Ascii,

    /// <summary>ASCII/Telnet UTF-8 terminal (80x24 mode)</summary>
    AsciiUtf8
}

