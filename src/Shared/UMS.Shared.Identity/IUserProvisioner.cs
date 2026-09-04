using UMS.Shared.ErrorHandling.Results;

namespace UMS.Shared.Identity;

/// <summary>
/// STU-2: lets another module provision a login-capable Identity <c>User</c> as a side effect of
/// its own creation (student requirement-spec.md §2: "an Identity <c>User</c> provisioning call
/// (login credential for the new student)"). Mirrors <c>IRecipientDirectory</c>'s own doc comment
/// exactly - living in <c>UMS.Shared.Identity</c>, not <c>UMS.Modules.Identity.*</c>, is what lets
/// a calling module (Student is the first) provision a <c>User</c> without a forbidden dependency
/// on Identity's Domain/Application/Infrastructure internals (module-boundaries.md, ADR-0002).
/// Identity's own Infrastructure layer registers the one real implementation, adapting this
/// interface onto <c>UserProvisioningService</c>.
/// </summary>
public interface IUserProvisioner
{
    /// <summary>
    /// Not idempotent on its own - a caller that needs "retry-safe creation" (Student's own
    /// <c>CreateStudentRecordService</c>) achieves that by only ever calling this from its own
    /// first-successful-insert branch (never from an idempotent lookup-and-return branch), so a
    /// retried caller-side operation never re-invokes this at all. A duplicate email/username
    /// collision (e.g. the same identifier provisioned twice through two different calling
    /// modules) still surfaces as <see cref="Error"/> here, exactly as <c>UserProvisioningService</c>'s
    /// own duplicate-identifier handling already does for Identity's own direct callers.
    /// </summary>
    public Task<Result<ProvisionedUserSummary>> ProvisionAsync(ProvisionUserCommand command, CancellationToken cancellationToken = default);
}

/// <summary>One caller's request to provision a User, exactly as it calls <see cref="IUserProvisioner.ProvisionAsync"/>.</summary>
/// <param name="Password">
/// A caller-generated, sufficiently random initial credential the provisioned person never sees
/// directly - the calling module is expected to direct them to Identity's own forgot-password flow
/// (IDN-12) to set their own password, communicated via the calling module's own welcome
/// notification. Mirrors how a real university onboarding flow works: an account exists before its
/// owner has ever chosen a password for it.
/// </param>
public sealed record ProvisionUserCommand(
    string Username,
    string Email,
    string GivenName,
    string FamilyName,
    string? GivenNameBn,
    string? FamilyNameBn,
    string? Mobile,
    string? UniversityId,
    string Password);

public sealed record ProvisionedUserSummary(Guid UserId, string Username, string Email);
