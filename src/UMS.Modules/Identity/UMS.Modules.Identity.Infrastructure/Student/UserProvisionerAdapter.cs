using UMS.Modules.Identity.Application.Users;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Identity;

namespace UMS.Modules.Identity.Infrastructure.Student;

/// <summary>
/// The one real implementation of <see cref="IUserProvisioner"/> (release/DEVELOPMENT_PLAN.md
/// Flow #11, STU-2) - delegates to Identity's own <see cref="UserProvisioningService"/>, the same
/// in-process, shared-interface pattern <c>UMS.Modules.Identity.Infrastructure.Notifications.RecipientDirectoryAdapter</c>
/// already established for <c>IRecipientDirectory</c>, so Student never takes a forbidden
/// dependency on <c>UMS.Modules.Identity.*</c> internals.
/// </summary>
internal sealed class UserProvisionerAdapter(UserProvisioningService userProvisioningService) : IUserProvisioner
{
    public async Task<Result<ProvisionedUserSummary>> ProvisionAsync(ProvisionUserCommand command, CancellationToken cancellationToken = default)
    {
        var request = new ProvisionUserRequest(
            command.Username,
            command.Email,
            command.GivenName,
            command.FamilyName,
            command.GivenNameBn,
            command.FamilyNameBn,
            command.Mobile,
            command.UniversityId,
            command.Password);

        var result = await userProvisioningService.ProvisionAsync(request, cancellationToken).ConfigureAwait(false);
        return result.Match<Result<ProvisionedUserSummary>>(
            dto => new ProvisionedUserSummary(dto.Id, dto.Username, dto.Email),
            error => error);
    }
}
