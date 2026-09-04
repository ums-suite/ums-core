namespace UMS.Modules.Student.Application.Abstractions;

/// <summary>
/// STU-2: Student's own local port onto <c>UMS.Shared.Identity.IUserProvisioner</c> - mirrors
/// Faculty's own <c>IOrganizationDepartmentExistenceChecker</c> local-port pattern, applied to an
/// outbound command instead of a read.
/// </summary>
public interface IUserProvisioningPort
{
    public Task<UserProvisioningOutcome> ProvisionAsync(ProvisionStudentUserRequest request, CancellationToken cancellationToken = default);
}

public sealed record ProvisionStudentUserRequest(
    string Username,
    string Email,
    string GivenName,
    string FamilyName,
    string? GivenNameBn,
    string? FamilyNameBn,
    string? Mobile);

/// <summary>Best-effort outcome (see <c>Student.IdentityUserId</c>'s own remarks) - never throws for an ordinary provisioning failure, only for a genuine infrastructure fault.</summary>
public sealed record UserProvisioningOutcome(bool Succeeded, Guid? UserId, string? FailureReason);
