using UMS.Modules.Identity.Domain.Common;

namespace UMS.Modules.Identity.Domain.Events;

/// <summary>
/// A failed authentication attempt (requirement-spec.md identity §3, "for lockout/security
/// monitoring"). Raised directly by the login application service rather than from a
/// <see cref="Users.User"/> instance - an unknown identifier never resolves to a loaded aggregate
/// at all, so this event carries the raw presented identifier instead of a <see cref="Users.UserId"/>.
/// </summary>
public sealed record UserLoginFailed(string Identifier, string Reason, DateTimeOffset OccurredAt) : IDomainEvent;
