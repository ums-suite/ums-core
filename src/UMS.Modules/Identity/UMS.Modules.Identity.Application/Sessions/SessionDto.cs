namespace UMS.Modules.Identity.Application.Sessions;

public sealed record SessionDto(
    Guid Id,
    string? UserAgent,
    string? CreatedFromIp,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastUsedAt,
    string Status,
    bool IsCurrent);
