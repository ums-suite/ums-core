using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Domain.Users;
using UMS.Shared.Domain;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Identity.Application.Users;

/// <summary>
/// IDN-2: provisions a new <see cref="User"/> - identifier resolution/validation, Credential
/// creation via adaptive password hashing, and the "same person, one identity" race-safety net
/// (requirement-spec.md identity §2/§3/§4/§8; edge-cases.md, "Concurrent provisioning creates a
/// duplicate User for the same person").
/// </summary>
public sealed class UserProvisioningService(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    private const int MinimumPasswordLength = 8;

    public static UserDto ToDto(User user) => new(
        user.Id.Value,
        user.Username,
        user.Email.Value,
        user.Name.DisplayName,
        user.Mobile?.Value,
        user.UniversityId,
        user.Status.ToString(),
        user.CreatedAt);

    public async Task<Result<UserDto>> ProvisionAsync(ProvisionUserRequest request, CancellationToken cancellationToken = default)
    {
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure)
        {
            return emailResult.Error!;
        }

        PhoneNumber? mobile = null;
        if (!string.IsNullOrWhiteSpace(request.Mobile))
        {
            var mobileResult = PhoneNumber.Create(request.Mobile);
            if (mobileResult.IsFailure)
            {
                return mobileResult.Error!;
            }

            mobile = mobileResult.Value;
        }

        var nameResult = PersonName.Create(request.GivenName, request.FamilyName, request.GivenNameBn, request.FamilyNameBn);
        if (nameResult.IsFailure)
        {
            return nameResult.Error!;
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return Error.Validation("user.username_required", "Username is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < MinimumPasswordLength)
        {
            return Error.Validation("user.password_too_short", $"Password must be at least {MinimumPasswordLength} characters.");
        }

        var now = clock.UtcNow;
        var credential = Credential.FromHash(passwordHasher.HashPassword(request.Password), passwordHasher.AlgorithmName, now);

        var user = User.Provision(
            request.Username,
            emailResult.Value,
            nameResult.Value,
            mobile,
            string.IsNullOrWhiteSpace(request.UniversityId) ? null : request.UniversityId.Trim(),
            credential,
            now);

        users.Add(user);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DuplicateUserException ex) when (ex.IdentifierType == "email")
        {
            // Edge case: two provisioning flows racing on the same stable identifier (email) both
            // see "no existing User" and both try to create one. The DB's unique constraint is the
            // authoritative safety net - the loser here re-queries and links to the winner's row
            // instead of surfacing a raw conflict (edge-cases.md decision).
            var existing = await users.GetByEmailAsync(emailResult.Value, cancellationToken).ConfigureAwait(false);
            return existing is null
                ? Error.Failure("user.provisioning_race_unresolved", "A conflicting User could not be resolved after a duplicate-email race.")
                : ToDto(existing);
        }
        catch (DuplicateUserException ex)
        {
            // A username/mobile/university-id collision is a genuine conflict between two
            // different people, not the "same person" case above - surfaced as-is. The
            // conflicting value is read back from this request (already known/validated here)
            // rather than from the database exception - EF/Npgsql do not reliably surface the
            // failed entity on every batched-command shape, so re-deriving it from what the
            // caller submitted is the only value guaranteed to be available.
            var conflictingValue = ex.IdentifierType switch
            {
                "username" => request.Username,
                "mobile" => request.Mobile,
                "university id" => request.UniversityId,
                _ => ex.IdentifierValue,
            };

            return Error.Conflict("user.duplicate_identifier", $"A User already exists with {ex.IdentifierType} '{conflictingValue}'.");
        }

        return ToDto(user);
    }
}
