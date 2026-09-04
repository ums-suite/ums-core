using UMS.Modules.Identity.Domain.Common;
using UMS.Modules.Identity.Domain.Sessions;
using UMS.Modules.Identity.Domain.Users;

namespace UMS.Modules.Identity.Domain.Events;

/// <summary>
/// A single-session revoke, "log out everywhere", or refresh-token-reuse compromise response
/// (requirement-spec.md identity §3). The shared cache-invalidation pipeline
/// (design-decisions.md, "Permission-Check Caching vs. Live Lookup") reuses this same event to
/// drive its Redis revoked-session-id fast path.
/// </summary>
public sealed record SessionRevoked(SessionId SessionId, UserId UserId, string Reason, DateTimeOffset OccurredAt) : IDomainEvent;
