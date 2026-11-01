namespace Bbs.Server.Adsb;

public sealed record Aircraft(
    string Id,
    string? Callsign,
    double Latitude,
    double Longitude,
    double? AltitudeFeet,
    double? GroundSpeedKnots,
    double? TrackDegrees,
    bool IsOnGround,
    DateTimeOffset ObservedAt);

public sealed record AircraftSnapshot(
    IReadOnlyList<Aircraft> Aircraft,
    DateTimeOffset? RetrievedAt,
    string? LastError)
{
    public static AircraftSnapshot Empty { get; } = new([], null, null);
}

public interface IAircraftSnapshotStore
{
    AircraftSnapshot Current { get; }
    void Set(AircraftSnapshot snapshot);
}

public sealed class AircraftSnapshotStore : IAircraftSnapshotStore
{
    private AircraftSnapshot _current = AircraftSnapshot.Empty;
    public AircraftSnapshot Current => Volatile.Read(ref _current);
    public void Set(AircraftSnapshot snapshot) => Volatile.Write(ref _current, snapshot);
}
