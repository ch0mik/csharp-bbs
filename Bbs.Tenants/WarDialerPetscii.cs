using Bbs.Terminals;
using Bbs.Tenants.Content.WarDialer;

namespace Bbs.Tenants;

public sealed class WarDialerPetscii : PetsciiThread
{
    private const string SessionStateKey = "session:war-dialer:state";
    internal TimeSpan DialDelay { get; set; } = TimeSpan.FromMilliseconds(45);

    public override async Task DoLoopAsync(CancellationToken cancellationToken = default)
    {
        var state = GetCustomObject(SessionStateKey) as WarDialerSessionState ?? new WarDialerSessionState();
        SetCustomObject(SessionStateKey, state);
        while (!cancellationToken.IsCancellationRequested)
        {
            Cls();
            Println("WARGAMES MODEM DIALER");
            Println("---------------------------------------");
            Println("S) SCAN FOR CARRIER TONES");
            Println($"V) VIEW FOUND SYSTEMS ({state.Found.Count})");
            Println(".) RETURN TO BBS");
            Print("OPTION: ");
            var input = Normalize(await ReadAsync(12, cancellationToken).ConfigureAwait(false));
            if (input is "." or "Q" or "QUIT" or "EXIT") return;
            if (input is "S" or "SCAN")
            {
                await ScanAsync(state, cancellationToken).ConfigureAwait(false);
                continue;
            }
            if (input is "V" or "VIEW")
            {
                await ViewAsync(state, cancellationToken).ConfigureAwait(false);
                continue;
            }
            Println("INVALID OPTION");
        }
    }

    private async Task ScanAsync(WarDialerSessionState state, CancellationToken token)
    {
        Cls();
        Println("SCAN PARAMETERS (ENTER = DEFAULT)");
        var area = await ReadNumberAsync("AREA CODE [311]: ", 311, token).ConfigureAwait(false);
        var prefix = await ReadNumberAsync("PREFIX [437]: ", 437, token).ConfigureAwait(false);
        var start = await ReadNumberAsync("START [8700]: ", 8700, token).ConfigureAwait(false);
        var end = await ReadNumberAsync("END [8750]: ", 8750, token).ConfigureAwait(false);
        if (!WarDialerSessionState.IsValidScan(area, prefix, start, end))
        {
            Println("INVALID RANGE (MAXIMUM 251 NUMBERS)");
            await WaitAsync(token).ConfigureAwait(false);
            return;
        }

        Cls();
        Println("SCANNING FOR CARRIER TONES");
        Println("---------------------------------------");
        for (var number = start; number <= end; number++)
        {
            var system = state.Check(area, prefix, number);
            Println($"{area:000}-{prefix:000}-{number:0000} {(system is null ? "...." : "CARRIER")}");
            if (system is not null) state.Add(system);
            await FlushAsync(token).ConfigureAwait(false);
            await Task.Delay(DialDelay, token).ConfigureAwait(false);
        }
        SetCustomObject(SessionStateKey, state);
        Println($"SCAN COMPLETE - {state.Found.Count} FOUND");
        await WaitAsync(token).ConfigureAwait(false);
    }

    private async Task ViewAsync(WarDialerSessionState state, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Cls();
            Println("NUMBERS WITH CARRIER TONES");
            Println("---------------------------------------");
            if (state.Found.Count == 0)
            {
                Println("NO SYSTEMS FOUND");
                await WaitAsync(token).ConfigureAwait(false);
                return;
            }
            for (var i = 0; i < state.Found.Count; i++)
            {
                var system = state.Found[i];
                Println($"{(char)('A' + i)}) {system.DisplayNumber}");
                Println($"   {system.Name}");
            }
            Println(".) BACK");
            Print("CONNECT: ");
            var input = Normalize(await ReadAsync(4, token).ConfigureAwait(false));
            if (input is "." or "Q" or "QUIT") return;
            if (input.Length != 1) continue;
            var index = input[0] - 'A';
            if (index < 0 || index >= state.Found.Count) continue;
            var selected = state.Found[index];
            Cls();
            Println($"ATD{selected.AreaCode:000}{selected.Prefix:000}{selected.Number:0000}");
            Println("CONNECT 1200");
            await FlushAsync(token).ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromMilliseconds(300), token).ConfigureAwait(false);
            if (selected.CanConnect && selected.Name == "WOPR")
            {
                await LaunchAsync(new WoprPetscii(), token).ConfigureAwait(false);
            }
            else
            {
                Println("REMOTE SYSTEM NOT IMPLEMENTED");
                await WaitAsync(token).ConfigureAwait(false);
            }
        }
    }

    private async Task<int> ReadNumberAsync(string prompt, int defaultValue, CancellationToken token)
    {
        Print(prompt);
        var input = (await ReadAsync(4, token).ConfigureAwait(false)).Trim();
        return input.Length == 0 ? defaultValue : int.TryParse(input, out var value) ? value : -1;
    }

    private async Task<string> ReadAsync(int maxLength, CancellationToken token)
    {
        await FlushAsync(token).ConfigureAwait(false);
        return await ReadLineAsync(maxLength, token).ConfigureAwait(false);
    }

    private async Task WaitAsync(CancellationToken token)
    {
        Print("PRESS ENTER: ");
        await ReadAsync(1, token).ConfigureAwait(false);
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
