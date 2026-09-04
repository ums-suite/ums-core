using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Domain.Users;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Identity.Application.Users;

/// <summary>IDN-3: User lookup/listing (requirement-spec.md identity §6 `GET /users/{id}`, `GET /users`).</summary>
public sealed class UserQueryService(IUserRepository users)
{
    public async Task<Result<UserDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(new UserId(id), cancellationToken).ConfigureAwait(false);
        return user is null
            ? Error.NotFound("user.not_found", $"No User exists with id '{id}'.")
            : UserProvisioningService.ToDto(user);
    }

    public async Task<UserListPage> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take, 1, 200);

        var items = await users.ListAsync(skip, take, cancellationToken).ConfigureAwait(false);
        var total = await users.CountAsync(cancellationToken).ConfigureAwait(false);

        return new UserListPage(items.Select(UserProvisioningService.ToDto).ToList(), total, skip, take);
    }
}
