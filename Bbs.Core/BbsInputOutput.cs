using System.Net.Sockets;
using System.Text;

namespace Bbs.Core;

public abstract class BbsInputOutput : IDisposable
{
    private const int FlushThreshold = 2048;

    protected readonly TcpClient Client;
    protected readonly NetworkStream Stream;

    private string _readBuffer = string.Empty;

    protected BbsInputOutput(TcpClient client)
    {
        Client = client;
        Stream = client.GetStream();
    }

    public bool LocalEcho { get; set; } = true;

    public bool QuoteMode { get; private set; }

    public virtual void SetQuoteMode(bool value) => QuoteMode = value;

    public virtual async Task<int> ReadKeyAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var key = await ReadByteAsync(cancellationToken).ConfigureAwait(false);

            // Telnet IAC sequence: ignore negotiation commands so they never leak as visible glyphs.
            if (key == 255)
            {
                var command = await ReadByteAsync(cancellationToken).ConfigureAwait(false);

                // Escaped 255 (IAC IAC): treat as literal 255 key.
                if (command == 255)
                {
                    key = 255;
                }
                else if (command is 251 or 252 or 253 or 254)
                {
                    // WILL/WONT/DO/DONT + option byte
                    _ = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
                    continue;
                }
                else if (command == 250)
                {
                    // SB ... IAC SE
                    while (true)
                    {
                        var sub = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
                        if (sub != 255)
                        {
                            continue;
                        }

                        var end = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
                        if (end == 240)
                        {
                            break;
                        }

                        if (end != 255)
                        {
                            // Unexpected sequence inside SB, keep scanning.
                            continue;
                        }
                    }

                    continue;
                }
                else
                {
                    // Unknown IAC command: skip it and continue.
                    continue;
                }
            }

            if (key == ReturnAlias()) key = 10;
            if (key == BackspaceAlias()) key = BackspaceKey();
            return key;
        }
    }

    private async Task<int> ReadByteAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[1];
        var read = await Stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (read <= 0)
        {
            throw new BbsIOException("BbsIOException::ReadKeyAsync()");
        }

        return buffer[0];
    }

    public virtual async Task<int> KeyPressedAsync(CancellationToken cancellationToken = default)
    {
        if (!Stream.DataAvailable)
        {
            return -1;
        }

        return await ReadKeyAsync(cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task<int> KeyPressedAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (timeout <= TimeSpan.Zero)
        {
            return await ReadKeyAsync(cancellationToken).ConfigureAwait(false);
        }

        var startedAt = DateTime.UtcNow;
        while (DateTime.UtcNow - startedAt < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = await KeyPressedAsync(cancellationToken).ConfigureAwait(false);
            if (key >= 0)
            {
                return key;
            }

            await Task.Delay(120, cancellationToken).ConfigureAwait(false);
        }

        return -1;
    }

    public async Task<string> ReadLineAsync(
        int maxLength = 0,
        bool mask = false,
        ISet<int>? allowedChars = null,
        bool sendCr = true,
        bool uppercase = false,
        bool noEmpty = false,
        bool interruptable = false,
        CancellationToken cancellationToken = default)
    {
        _readBuffer = string.Empty;

        while (true)
        {
            var ch = await ReadKeyAsync(cancellationToken).ConfigureAwait(false);

            if (noEmpty && IsNewline(ch) && string.IsNullOrWhiteSpace(_readBuffer))
            {
                continue;
            }

            if (IsBackspace(ch))
            {
                if (_readBuffer.Length > 0)
                {
                    _readBuffer = _readBuffer[..^1];
                    if (LocalEcho)
                    {
                        Write(Backspace());
                        await FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                }

                continue;
            }

            if (interruptable && ch == 3)
            {
                return string.Empty;
            }

            if (allowedChars is not null && !allowedChars.Contains(ch) && !IsNewline(ch))
            {
                continue;
            }

            if (IsNewline(ch))
            {
                if (LocalEcho && sendCr)
                {
                    await NewlineAsync(cancellationToken).ConfigureAwait(false);
                }

                break;
            }

            if (!IsPrintableChar(ch))
            {
                continue;
            }

            if (maxLength > 0 && _readBuffer.Length >= maxLength)
            {
                continue;
            }

            var normalized = ConvertToAscii(ch);
            _readBuffer += (char)normalized;

            if (LocalEcho)
            {
                if (mask)
                {
                    Write((byte)'*');
                }
                else if (uppercase && char.IsLetter((char)normalized))
                {
                    Write((byte)char.ToUpperInvariant((char)normalized));
                }
                else
                {
                    Write((byte)normalized);
                }

                await FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        var result = uppercase ? _readBuffer.ToUpperInvariant() : _readBuffer;
        _readBuffer = string.Empty;
        return result;
    }

    public string ReadLineBuffer() => _readBuffer;

    public async Task<string> ReadPasswordAsync(CancellationToken cancellationToken = default)
        => await ReadLineAsync(mask: true, cancellationToken: cancellationToken).ConfigureAwait(false);

    public virtual async Task<byte[]> ResetInputAsync(CancellationToken cancellationToken = default)
    {
        var data = new List<byte>(FlushThreshold);

        while (Stream.DataAvailable && data.Count < FlushThreshold)
        {
            var value = await ReadKeyAsync(cancellationToken).ConfigureAwait(false);
            data.Add((byte)value);
        }

        return data.ToArray();
    }

    public virtual void Print(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        foreach (var ch in message)
        {
            Write((byte)ch);
        }
    }

    public virtual void PrintRaw(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        var bytes = Encoding.Latin1.GetBytes(message);
        Write(bytes);
    }

    public virtual void Println(string? message)
    {
        Print(message);
        Write(NewlineBytes());
    }

    public virtual void PrintlnRaw(string? message)
    {
        PrintRaw(message);
        Write(NewlineBytes());
    }

    public virtual Task NewlineAsync(CancellationToken cancellationToken = default)
    {
        Write(NewlineBytes());
        return FlushAsync(cancellationToken);
    }

    public virtual void Write(byte b)
    {
        if (b == (byte)'"')
        {
            QuoteMode = !QuoteMode;
        }
        else if (b == 13 || b == 141)
        {
            QuoteMode = false;
        }

        Stream.WriteByte(b);
    }

    public virtual void Write(params int[] bytes)
    {
        foreach (var b in bytes)
        {
            Write((byte)b);
        }
    }

    public virtual void Write(byte[] bytes)
    {
        foreach (var b in bytes)
        {
            Write(b);
        }
    }

    public virtual Task FlushAsync(CancellationToken cancellationToken = default)
    {
        return Stream.FlushAsync(cancellationToken);
    }

    public virtual bool IsPrintableChar(int c) => c >= 32;

    public virtual bool IsPrintableChar(char c) => IsPrintableChar((int)c);

    public virtual string FilterPrintable(string? value)
    {
        var source = value ?? string.Empty;
        return new string(source.Where(IsPrintableChar).ToArray());
    }

    public virtual string FilterPrintableWithNewline(string? value)
    {
        var source = value ?? string.Empty;
        return new string(source.Where(ch => IsPrintableChar(ch) || ch is '\n' or '\r').ToArray());
    }

    public virtual int ReturnAlias() => 10;

    public virtual int BackspaceAlias() => 8;

    public abstract byte[] NewlineBytes();

    public abstract int BackspaceKey();

    public abstract byte[] Backspace();

    public abstract bool IsNewline(int ch);

    public abstract bool IsBackspace(int ch);

    public abstract int ConvertToAscii(int ch);

    public virtual void OptionalCls()
    {
    }

    public virtual void Shutdown()
    {
        Stream.Close();
        Client.Close();
    }

    public void Dispose()
    {
        Shutdown();
        GC.SuppressFinalize(this);
    }
}

