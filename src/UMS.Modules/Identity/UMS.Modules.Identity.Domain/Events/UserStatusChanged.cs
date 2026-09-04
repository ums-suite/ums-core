using UMS.Modules.Identity.Domain.Common;
using UMS.Modules.Identity.Domain.Users;

namespace UMS.Modules.Identity.Domain.Events;

/// <summary>Suspend/reactivate a User (requirement-spec.md identity §3).</summary>
public sealed record UserStatusChanged(UserId UserId, UserStatus NewStatus, DateTimeOffset OccurredAt) : IDomainEvent;
