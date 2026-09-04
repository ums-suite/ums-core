using UMS.Modules.Identity.Domain.Common;
using UMS.Modules.Identity.Domain.Roles;
using UMS.Modules.Identity.Domain.Users;

namespace UMS.Modules.Identity.Domain.Events;

/// <summary>Role (optionally ScopeGrant-bound) assigned to a User (requirement-spec.md identity §3).</summary>
public sealed record RoleAssigned(UserId UserId, RoleId RoleId, Guid? OrganizationNodeId, DateTimeOffset OccurredAt) : IDomainEvent;
