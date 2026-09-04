using Microsoft.EntityFrameworkCore;
using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Domain.Users;
using UMS.Modules.Identity.Infrastructure.Persistence;
using UMS.Shared.Domain;

namespace UMS.Modules.Identity.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(IdentityDbContext context) : IUserRepository
{
    public Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken = default) =>
        context.Users.Include(u => u.RoleAssignments).FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default) =>
        context.Users.Include(u => u.RoleAssignments).FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<User?> GetByPasswordResetTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        context.Users.Include(u => u.RoleAssignments).FirstOrDefaultAsync(u => u.ResetChallenge!.TokenHash == tokenHash, cancellationToken);

    /// <summary>
    /// Resolves username, email, mobile, or university id in one query (requirement-spec.md
    /// identity §2). Every column is <c>citext</c> (case-insensitive) except <see cref="Email"/>/
    /// <see cref="PhoneNumber"/>, which are compared as their own value-converted type rather than
    /// as raw strings so the same converter EF uses for storage also governs the comparison.
    /// </summary>
    public Task<User?> GetByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return Task.FromResult<User?>(null);
        }

        var value = identifier.Trim();
        var emailResult = Email.Create(value);
        var mobileResult = PhoneNumber.Create(value);

        var emailMatches = emailResult.IsSuccess;
        var email = emailMatches ? emailResult.Value : null;
        var mobileMatches = mobileResult.IsSuccess;
        var mobile = mobileMatches ? mobileResult.Value : null;

        return context.Users
            .Include(u => u.RoleAssignments)
            .FirstOrDefaultAsync(
                u => u.Username == value
                    || (emailMatches && u.Email == email)
                    || (mobileMatches && u.Mobile == mobile)
                    || u.UniversityId == value,
                cancellationToken);
    }

    public async Task<IReadOnlyList<User>> ListAsync(int skip, int take, CancellationToken cancellationToken = default) =>
        await context.Users
            .OrderBy(u => u.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        context.Users.CountAsync(cancellationToken);

    public void Add(User user) => context.Users.Add(user);
}
