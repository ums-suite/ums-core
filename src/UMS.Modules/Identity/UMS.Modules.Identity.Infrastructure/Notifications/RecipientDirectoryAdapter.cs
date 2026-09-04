using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Domain.Users;
using UMS.Shared.Identity;

namespace UMS.Modules.Identity.Infrastructure.Notifications;

/// <summary>
/// The one real implementation of <see cref="IRecipientDirectory"/> (release/DEVELOPMENT_PLAN.md
/// Flow #8, NTF-2) - delegates to Identity's own <see cref="IUserRepository"/>, the same in-process,
/// shared-interface pattern <see cref="UMS.Shared.Audit.IAuditRecorder"/> and
/// <see cref="UMS.Shared.Organization.IOrganizationNodeExistenceChecker"/> already established
/// (ADR-0003, module-boundaries.md) so Notifications never takes a forbidden dependency on
/// <c>UMS.Modules.Identity.*</c> internals.
/// </summary>
internal sealed class RecipientDirectoryAdapter(IUserRepository users) : IRecipientDirectory
{
    public async Task<RecipientContactInfo?> GetContactInfoAsync(Guid recipientId, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(new UserId(recipientId), cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return null;
        }

        // PushToken/PreferredLanguageCode: see RecipientContactInfo's own remarks for why these are
        // a fixed null/"en" today rather than read from User - Identity has neither field yet.
        return new RecipientContactInfo(user.Email, user.Mobile, PushToken: null, PreferredLanguageCode: "en");
    }
}
