using UMS.Modules.Identity.Domain.Common;
using UMS.Modules.Identity.Domain.Users;

namespace UMS.Modules.Identity.Domain.Events;

/// <summary>A pending TOTP secret was verified and promoted to the User's active MFA credential (requirement-spec.md identity §3).</summary>
public sealed record MfaEnrolled(UserId UserId, DateTimeOffset OccurredAt) : IDomainEvent;
