using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Domain.Common;
using UMS.Modules.Identity.Domain.Roles;
using UMS.Modules.Identity.Domain.ScopeGrants;
using UMS.Modules.Identity.Domain.Sessions;
using UMS.Modules.Identity.Domain.Users;
using UMS.Shared.Domain;

namespace UMS.Modules.Identity.UnitTests.TestDoubles;

/// <summary>
/// Hand-rolled in-memory fakes for the Application-layer ports - no mocking library dependency,
/// consistent with this repo's "no premature abstraction" house style. True concurrency (the
/// actual DB-level race these fakes only approximate) is covered separately by the integration
/// test suite against a real Postgres instance.
/// </summary>
internal sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;
}

internal sealed class FakeUserRepository : IUserRepository
{
    public List<User> Users { get; } = [];

    public Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

    public Task<User?> GetByIdentifierAsync(string identifier, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(u =>
            string.Equals(u.Username, identifier, StringComparison.OrdinalIgnoreCase)
            || string.Equals(u.Email.Value, identifier, StringComparison.OrdinalIgnoreCase)));

    public Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Email == email));

    public Task<IReadOnlyList<User>> ListAsync(int skip, int take, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<User>>(Users.Skip(skip).Take(take).ToList());

    public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(Users.Count);

    public void Add(User user) => Users.Add(user);
}

internal sealed class FakeRoleRepository : IRoleRepository
{
    public List<Role> Roles { get; } = [];

    public Task<Role?> GetByIdAsync(RoleId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Roles.FirstOrDefault(r => r.Id == id));

    public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult(Roles.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<Role>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Role>>(Roles.ToList());

    public Task<IReadOnlyList<Role>> GetByIdsAsync(IEnumerable<RoleId> ids, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Role>>(Roles.Where(r => ids.Contains(r.Id)).ToList());

    public void Add(Role role) => Roles.Add(role);
}

internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string AlgorithmName => "fake";

    public string HashPassword(string plaintextPassword) => $"hashed:{plaintextPassword}";

    public bool VerifyPassword(string plaintextPassword, string hash) => hash == $"hashed:{plaintextPassword}";
}

internal sealed class FakeOrganizationNodeExistenceChecker(bool exists = true) : IOrganizationNodeExistenceChecker
{
    public Task<bool> ExistsAsync(OrganizationNodeId organizationNodeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(exists);
}

internal sealed class FakeSessionRepository : ISessionRepository
{
    public List<Session> Sessions { get; } = [];

    public Task<Session?> GetByIdAsync(SessionId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Sessions.FirstOrDefault(s => s.Id == id));

    public Task<IReadOnlyList<Session>> ListActiveForUserAsync(UserId userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Session>>(Sessions.Where(s => s.UserId == userId && s.Status == SessionStatus.Active).ToList());

    public void Add(Session session) => Sessions.Add(session);
}

/// <summary>Deterministic fake - hashes are just the plaintext prefixed, so tests can assert on them directly without a real crypto dependency.</summary>
internal sealed class FakeTokenService : ITokenService
{
    private int _refreshCounter;

    public IssuedAccessToken IssueAccessToken(UserId userId, SessionId sessionId, IReadOnlyCollection<string> roleNames, DateTimeOffset now) =>
        new($"access-token-for-{userId.Value:N}", now.AddMinutes(15));

    public IssuedRefreshToken IssueRefreshToken(SessionId sessionId, DateTimeOffset now)
    {
        var plaintext = $"{sessionId.Value:N}.secret-{++_refreshCounter}";
        return new IssuedRefreshToken(plaintext, HashRefreshToken(plaintext), now.AddDays(14));
    }

    public string HashRefreshToken(string plaintextRefreshToken) => $"hash-of:{plaintextRefreshToken}";

    public bool TryExtractSessionId(string plaintextRefreshToken, out SessionId sessionId)
    {
        sessionId = default;
        var separator = plaintextRefreshToken.IndexOf('.');
        if (separator <= 0 || !Guid.TryParseExact(plaintextRefreshToken[..separator], "N", out var guid))
        {
            return false;
        }

        sessionId = new SessionId(guid);
        return true;
    }
}

internal sealed class FakeAuthzCache : IAuthzCache
{
    public List<UserId> InvalidatedUsers { get; } = [];

    public List<SessionId> RevokedSessions { get; } = [];

    public Task InvalidateUserAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        InvalidatedUsers.Add(userId);
        return Task.CompletedTask;
    }

    public Task RevokeSessionAsync(SessionId sessionId, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        RevokedSessions.Add(sessionId);
        return Task.CompletedTask;
    }
}

internal sealed class FakeDomainEventRecorder : IDomainEventRecorder
{
    public List<IDomainEvent> Recorded { get; } = [];

    public void Enqueue(IDomainEvent domainEvent) => Recorded.Add(domainEvent);
}

/// <summary>
/// Optionally throws a queued <see cref="DuplicateUserException"/> on the next call, so a test can
/// exercise <c>UserProvisioningService</c>'s catch/translate branch without a real database.
/// </summary>
internal sealed class FakeUnitOfWork : IUnitOfWork
{
    private readonly Queue<DuplicateUserException> _queuedFailures = new();

    public int SaveChangesCallCount { get; private set; }

    public void QueueDuplicateUserFailure(DuplicateUserException exception) => _queuedFailures.Enqueue(exception);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        if (_queuedFailures.Count > 0)
        {
            throw _queuedFailures.Dequeue();
        }

        return Task.FromResult(1);
    }

    public Task<IUmsTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IUmsTransaction>(new FakeUmsTransaction());
}

/// <summary>
/// A no-op stand-in for a real ADO.NET transaction - no existing unit test drives a scenario that
/// calls <see cref="IUnitOfWork.BeginTransactionAsync"/> (that path is covered by a real Postgres
/// transaction in the integration test suite instead), but <see cref="FakeUnitOfWork"/> still needs
/// a concrete <see cref="IUmsTransaction"/> to satisfy the interface.
/// </summary>
internal sealed class FakeUmsTransaction : IUmsTransaction
{
    public System.Data.Common.DbTransaction DbTransaction => throw new NotSupportedException(
        "FakeUmsTransaction has no real underlying transaction - a unit test that needs one should not use FakeUnitOfWork.");

    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
