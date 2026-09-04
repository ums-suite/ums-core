using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Application.Users;
using UMS.Modules.Identity.Domain.Users;
using UMS.Modules.Identity.UnitTests.TestDoubles;
using UMS.Shared.Domain;

namespace UMS.Modules.Identity.UnitTests.Users;

public class UserProvisioningServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static ProvisionUserRequest ValidRequest(string email = "new.user@example.edu.bd", string username = "new.user") =>
        new(username, email, "New", "User", null, null, null, null, "a-strong-password");

    [Fact]
    public async Task ProvisionAsync_succeeds_and_hashes_the_password()
    {
        var users = new FakeUserRepository();
        var hasher = new FakePasswordHasher();
        var service = new UserProvisioningService(users, hasher, new FakeUnitOfWork(), new FakeClock(_now));

        var result = await service.ProvisionAsync(ValidRequest());

        Assert.True(result.IsSuccess);
        Assert.Single(users.Users);
        Assert.Equal("hashed:a-strong-password", users.Users[0].Credential.PasswordHash);
    }

    [Fact]
    public async Task ProvisionAsync_rejects_a_password_shorter_than_the_minimum()
    {
        var service = new UserProvisioningService(new FakeUserRepository(), new FakePasswordHasher(), new FakeUnitOfWork(), new FakeClock(_now));

        var result = await service.ProvisionAsync(ValidRequest() with { Password = "short" });

        Assert.False(result.IsSuccess);
        Assert.Equal("user.password_too_short", result.Error!.Code);
    }

    [Fact]
    public async Task ProvisionAsync_rejects_an_invalid_email()
    {
        var service = new UserProvisioningService(new FakeUserRepository(), new FakePasswordHasher(), new FakeUnitOfWork(), new FakeClock(_now));

        var result = await service.ProvisionAsync(ValidRequest() with { Email = "not-an-email" });

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ProvisionAsync_on_a_duplicate_email_race_links_to_the_already_committed_User_instead_of_failing()
    {
        // Edge-cases.md, "Concurrent provisioning creates a duplicate User for the same person":
        // the losing side of the DB race gets DuplicateUserException("email", ...) and must
        // transparently resolve to the winner's already-persisted row.
        var users = new FakeUserRepository();
        var existingCredential = Credential.FromHash("hashed:a-strong-password", "fake", _now);
        var winner = User.Provision("existing.user", Email.Create("new.user@example.edu.bd").Value, PersonName.Create("Existing", "User").Value, null, null, existingCredential, _now);
        users.Users.Add(winner);

        var unitOfWork = new FakeUnitOfWork();
        unitOfWork.QueueDuplicateUserFailure(new DuplicateUserException("email", "new.user@example.edu.bd"));
        var service = new UserProvisioningService(users, new FakePasswordHasher(), unitOfWork, new FakeClock(_now));

        var result = await service.ProvisionAsync(ValidRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(winner.Id.Value, result.Value.Id);
    }

    [Fact]
    public async Task ProvisionAsync_on_a_duplicate_username_race_surfaces_a_genuine_conflict()
    {
        // A username collision is two DIFFERENT people wanting the same identifier - not the
        // "same person" case above - so it must surface as a real conflict, never silently linked.
        var unitOfWork = new FakeUnitOfWork();
        unitOfWork.QueueDuplicateUserFailure(new DuplicateUserException("username", "new.user"));
        var service = new UserProvisioningService(new FakeUserRepository(), new FakePasswordHasher(), unitOfWork, new FakeClock(_now));

        var result = await service.ProvisionAsync(ValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal("user.duplicate_identifier", result.Error!.Code);
    }
}
