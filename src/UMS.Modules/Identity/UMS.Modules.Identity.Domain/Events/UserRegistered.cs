using UMS.Modules.Identity.Domain.Common;
using UMS.Modules.Identity.Domain.Users;

namespace UMS.Modules.Identity.Domain.Events;

/// <summary>New <see cref="User"/> created (requirement-spec.md identity §3).</summary>
public sealed record UserRegistered(UserId UserId, string Email, DateTimeOffset OccurredAt) : IDomainEvent;
