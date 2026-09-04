using Microsoft.Extensions.Options;
using UMS.Modules.Identity.Application;
using UMS.Modules.Identity.Application.Auth;
using UMS.Modules.Identity.Domain.Users;
using UMS.Modules.Identity.UnitTests.TestDoubles;
using UMS.Shared.Domain;

namespace UMS.Modules.Identity.UnitTests.Auth;

public class AuthenticationServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static AuthenticationService BuildService(FakeUserRepository users, FakeSessionRepository sessions, FakeDomainEventRecorder recorder, FakeUnitOfWork unitOfWork) =>
        new(users, new FakeRoleRepository(), sessions, new FakePasswordHasher(), new FakeTokenService(), unitOfWork, recorder, new FakeClock(_now), Options.Create(new IdentityTokenOptions()));

    private static User SeedActiveUser(FakeUserRepository users, string password = "correct-password")
    {
        var hasher = new FakePasswordHasher();
        var credential = Credential.FromHash(hasher.HashPassword(password), hasher.AlgorithmName, _now);
        var user = User.Provision("jane.doe", Email.Create("jane.doe@example.edu.bd").Value, PersonName.Create("Jane", "Doe").Value, null, null, credential, _now);
        users.Users.Add(user);
        return user;
    }

    [Fact]
    public async Task LoginAsync_with_correct_credentials_issues_a_token_pair_and_opens_a_Session()
    {
        var users = new FakeUserRepository();
        SeedActiveUser(users);
        var sessions = new FakeSessionRepository();
        var service = BuildService(users, sessions, new FakeDomainEventRecorder(), new FakeUnitOfWork());

        var result = await service.LoginAsync(new LoginRequest("jane.doe", "correct-password", null, null));

        Assert.True(result.IsSuccess);
        Assert.Single(sessions.Sessions);
        Assert.Equal(sessions.Sessions[0].Id.Value, result.Value.SessionId);
    }

    [Fact]
    public async Task LoginAsync_with_the_wrong_password_fails_generically_and_records_UserLoginFailed()
    {
        var users = new FakeUserRepository();
        SeedActiveUser(users);
        var recorder = new FakeDomainEventRecorder();
        var service = BuildService(users, new FakeSessionRepository(), recorder, new FakeUnitOfWork());

        var result = await service.LoginAsync(new LoginRequest("jane.doe", "wrong-password", null, null));

        Assert.False(result.IsSuccess);
        Assert.Equal("auth.invalid_credentials", result.Error!.Code);
        Assert.Single(recorder.Recorded);
    }

    [Fact]
    public async Task LoginAsync_with_an_unknown_identifier_fails_with_the_same_generic_error_as_a_wrong_password()
    {
        var service = BuildService(new FakeUserRepository(), new FakeSessionRepository(), new FakeDomainEventRecorder(), new FakeUnitOfWork());

        var result = await service.LoginAsync(new LoginRequest("no-such-user", "anything", null, null));

        Assert.False(result.IsSuccess);
        Assert.Equal("auth.invalid_credentials", result.Error!.Code);
    }

    [Fact]
    public async Task LoginAsync_for_a_suspended_user_fails_even_with_the_correct_password()
    {
        var users = new FakeUserRepository();
        var user = SeedActiveUser(users);
        user.Suspend(_now);
        var service = BuildService(users, new FakeSessionRepository(), new FakeDomainEventRecorder(), new FakeUnitOfWork());

        var result = await service.LoginAsync(new LoginRequest("jane.doe", "correct-password", null, null));

        Assert.False(result.IsSuccess);
        Assert.Equal("auth.user_inactive", result.Error!.Code);
    }
}
