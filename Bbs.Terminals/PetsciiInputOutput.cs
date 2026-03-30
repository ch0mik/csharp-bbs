using Bbs.Core;
using System.Net.Sockets;

namespace Bbs.Terminals;

public sealed class PetsciiInputOutput : BbsInputOutput
{
    public PetsciiInputOutput(TcpClient client) : base(client)
    {
    }

    public override byte[] NewlineBytes() => new byte[] { 13 };

    public override int BackspaceKey() => PetsciiKeys.Del;

    public override byte[] Backspace() => new byte[] { PetsciiKeys.Del };

    public override bool IsNewline(int ch) => ch == PetsciiKeys.Return || ch == 141;

    public override bool IsBackspace(int ch) => ch == PetsciiKeys.Del || ch == PetsciiKeys.Ins;

    public override int ConvertToAscii(int ch)
    {
        if (ch >= 193 && ch <= 218)
        {
            ch -= 96;
        }

        // Convert case for PETSCII display
        if (ch >= 'a' && ch <= 'z') return char.ToUpperInvariant((char)ch);
        if (ch >= 'A' && ch <= 'Z') return char.ToLowerInvariant((char)ch);
        if (ch == 164) return '_';
        return ch;
    }

    public override bool IsPrintableChar(int c)
        => (c >= 32 && c <= 127) || (c >= 160 && c <= 255);

    public override async Task<int> ReadKeyAsync(CancellationToken cancellationToken = default)
    {
        var result = await base.ReadKeyAsync(cancellationToken).ConfigureAwait(false);
        if (result >= 193 && result <= 218)
        {
            return result - 96;
        }

        return result;
    }

    public override void Print(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        var previousWasCr = false;
        foreach (var source in message)
        {
            if (source == '\r')
            {
                Write((byte)PetsciiKeys.Return);
                previousWasCr = true;
                continue;
            }

            if (source == '\n')
            {
                if (!previousWasCr)
                {
                    Write((byte)PetsciiKeys.Return);
                }

                previousWasCr = false;
                continue;
            }

            var c = source;
            if (!IsPrintableChar(c))
            {
                continue;
            }

            if (c == '_') c = (char)228;
            else if (c >= 'a' && c <= 'z') c = char.ToUpperInvariant(c);
            else if (c >= 'A' && c <= 'Z') c = char.ToLowerInvariant(c);

            Write((byte)c);
            previousWasCr = false;
        }
    }
}
