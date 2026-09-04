using System.Security.Cryptography;
using UMS.Modules.Student.Application.Abstractions;
using UMS.Shared.Identity;

namespace UMS.Modules.Student.Infrastructure.CrossModule;

/// <summary>STU-2: adapts <c>UMS.Shared.Identity.IUserProvisioner</c> to Student's own local port - the one call site that generates the caller-side initial password (<c>UMS.Shared.Identity.ProvisionUserCommand.Password</c>'s own remarks), since that mechanical detail belongs to the Infrastructure layer, not the Application-level command shape.</summary>
internal sealed class UserProvisioningPortAdapter(IUserProvisioner userProvisioner) : IUserProvisioningPort
{
    public async Task<UserProvisioningOutcome> ProvisionAsync(ProvisionStudentUserRequest request, CancellationToken cancellationToken = default)
    {
        var command = new ProvisionUserCommand(
            request.Username,
            request.Email,
            request.GivenName,
            request.FamilyName,
            request.GivenNameBn,
            request.FamilyNameBn,
            request.Mobile,
            UniversityId: null,
            Password: GenerateInitialPassword());

        var result = await userProvisioner.ProvisionAsync(command, cancellationToken).ConfigureAwait(false);
        return result.Match(
            summary => new UserProvisioningOutcome(true, summary.UserId, null),
            error => new UserProvisioningOutcome(false, null, error.Message));
    }

    private static string GenerateInitialPassword() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
}
