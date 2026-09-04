using UMS.Modules.Identity.Domain.Common;
using UMS.Modules.Identity.Domain.Sessions;
using UMS.Modules.Identity.Domain.Users;

namespace UMS.Modules.Identity.Domain.Events;

/// <summary>Successful authentication (requirement-spec.md identity §3).</summary>
public sealed record UserLoggedIn(UserId UserId, SessionId SessionId, DateTimeOffset OccurredAt) : IDomainEvent;
