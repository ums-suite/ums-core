using UMS.Modules.Identity.Domain.Common;
using UMS.Modules.Identity.Domain.Users;

namespace UMS.Modules.Identity.Domain.Events;

/// <summary>Credential change (requirement-spec.md identity §3).</summary>
public sealed record PasswordChanged(UserId UserId, DateTimeOffset OccurredAt) : IDomainEvent;
