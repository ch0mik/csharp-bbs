namespace Bbs.Core;

public sealed record ClientSnapshot(
    long ClientId,
    string ClientName,
    string ClientClass,
    string ClientIp,
    int ServerPort,
    DateTimeOffset StartedAt,
    DateTimeOffset LastActivityAt);
