namespace Bbs.Tenants.Content.WarDialer;

internal sealed record DialedSystem(int AreaCode, int Prefix, int Number, string Name, bool CanConnect = false)
{
    public string DisplayNumber => $"({AreaCode:000}) {Prefix:000}-{Number:0000}";
}

internal sealed class WarDialerSessionState
{
    private static readonly DialedSystem[] Catalog =
    [
        new(311, 437, 1083, "ARPANET"),
        new(311, 936, 1493, "INTERNET"),
        new(311, 399, 2364, "BANK"),
        new(311, 437, 2977, "TELSTAR"),
        new(311, 767, 3395, "NIGHTOWL-BBS"),
        new(311, 399, 3572, "PAN-AM"),
        new(311, 936, 3923, "SIMULANT"),
        new(311, 767, 7305, "SYSTEM"),
        new(311, 437, 8739, "WOPR", true)
    ];

    public List<DialedSystem> Found { get; } = new();

    public DialedSystem? Check(int areaCode, int prefix, int number)
        => Catalog.FirstOrDefault(s => s.AreaCode == areaCode && s.Prefix == prefix && s.Number == number);

    public bool Add(DialedSystem system)
    {
        if (Found.Any(s => s.AreaCode == system.AreaCode && s.Prefix == system.Prefix && s.Number == system.Number)) return false;
        Found.Add(system);
        return true;
    }

    public static bool IsValidScan(int areaCode, int prefix, int start, int end)
        => areaCode is >= 1 and <= 999
           && prefix is >= 0 and <= 999
           && start is >= 1 and <= 9999
           && end >= start && end <= 9999
           && end - start <= 250;
}
