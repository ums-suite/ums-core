using UMS.Modules.Identity.Application.Roles;
using UMS.Modules.Identity.Domain.Roles;
using UMS.Modules.Identity.Domain.Users;
using UMS.Modules.Identity.UnitTests.TestDoubles;
using UMS.Shared.Domain;

namespace UMS.Modules.Identity.UnitTests.Roles;

public class RoleAssignmentServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (FakeUserRepository Users, User User) SeedUser()
    {
        var users = new FakeUserRepository();
        var credential = Credential.FromHash("hash", "fake", _now);
        var user = User.Provision("jane.doe", Email.Create("jane.doe@example.edu.bd").Value, PersonName.Create("Jane", "Doe").Value, null, null, credential, _now);
        users.Users.Add(user);
        return (users, user);
    }

    [Fact]
    public async Task AssignAsync_succeeds_and_invalidates_the_authz_cache_for_the_user()
    {
        var (users, user) = SeedUser();
        var roles = new FakeRoleRepository();
        var role = Role.Create("Registrar", null, ["identity.user.manage"], _now);
        roles.Roles.Add(role);
        var authzCache = new FakeAuthzCache();

        var service = new RoleAssignmentService(users, roles, new FakeOrganizationNodeExistenceChecker(), new FakeUnitOfWork(), authzCache, new FakeClock(_now));
        var result = await service.AssignAsync(user.Id.Value, new AssignRoleRequest(role.Id.Value, null));

        Assert.True(result.IsSuccess);
        Assert.Contains(user.Id, authzCache.InvalidatedUsers);
    }

    [Fact]
    public async Task AssignAsync_rejects_a_ScopeGrant_referencing_a_nonexistent_OrganizationNode()
    {
        var (users, user) = SeedUser();
        var roles = new FakeRoleRepository();
        var role = Role.Create("Department Head", null, [], _now);
        roles.Roles.Add(role);

        var service = new RoleAssignmentService(users, roles, new FakeOrganizationNodeExistenceChecker(exists: false), new FakeUnitOfWork(), new FakeAuthzCache(), new FakeClock(_now));
        var result = await service.AssignAsync(user.Id.Value, new AssignRoleRequest(role.Id.Value, Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal("role.scope_grant_invalid_organization_node", result.Error!.Code);
    }

    [Fact]
    public async Task AssignAsync_returns_NotFound_for_an_unknown_role()
    {
        var (users, user) = SeedUser();
        var service = new RoleAssignmentService(users, new FakeRoleRepository(), new FakeOrganizationNodeExistenceChecker(), new FakeUnitOfWork(), new FakeAuthzCache(), new FakeClock(_now));

        var result = await service.AssignAsync(user.Id.Value, new AssignRoleRequest(Guid.NewGuid(), null));

        Assert.False(result.IsSuccess);
        Assert.Equal("role.not_found", result.Error!.Code);
    }

    [Fact]
    public async Task AssignAsync_returns_Conflict_when_the_same_role_and_scope_is_already_assigned()
    {
        var (users, user) = SeedUser();
        var roles = new FakeRoleRepository();
        var role = Role.Create("Registrar", null, [], _now);
        roles.Roles.Add(role);
        user.AssignRole(role.Id, null, _now);

        var service = new RoleAssignmentService(users, roles, new FakeOrganizationNodeExistenceChecker(), new FakeUnitOfWork(), new FakeAuthzCache(), new FakeClock(_now));
        var result = await service.AssignAsync(user.Id.Value, new AssignRoleRequest(role.Id.Value, null));

        Assert.False(result.IsSuccess);
        Assert.Equal("role.already_assigned", result.Error!.Code);
    }

    [Fact]
    public async Task RevokeAsync_invalidates_the_authz_cache_so_the_next_permission_check_sees_the_revoke()
    {
        // "Role revoked mid-session" (edge-cases.md).
        var (users, user) = SeedUser();
        var role = Role.Create("Registrar", null, [], _now);
        var assignment = user.AssignRole(role.Id, null, _now);
        var authzCache = new FakeAuthzCache();

        var service = new RoleAssignmentService(users, new FakeRoleRepository(), new FakeOrganizationNodeExistenceChecker(), new FakeUnitOfWork(), authzCache, new FakeClock(_now));
        var result = await service.RevokeAsync(user.Id.Value, assignment.Id.Value);

        Assert.True(result.IsSuccess);
        Assert.False(assignment.IsActive);
        Assert.Contains(user.Id, authzCache.InvalidatedUsers);
    }
}
