using UMS.Modules.Identity.Domain.Common;
using UMS.Modules.Identity.Domain.Roles;
using UMS.Modules.Identity.Domain.Users;

namespace UMS.Modules.Identity.Domain.Events;

/// <summary>Role assignment revoked (requirement-spec.md identity §3) - drives the same live cache-invalidation pipeline as <see cref="SessionRevoked"/>.</summary>
public sealed record RoleRevoked(UserId UserId, RoleId RoleId, Guid? OrganizationNodeId, DateTimeOffset OccurredAt) : IDomainEvent;
