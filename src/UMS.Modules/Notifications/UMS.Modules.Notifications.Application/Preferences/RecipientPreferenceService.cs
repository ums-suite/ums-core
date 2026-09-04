using UMS.Modules.Notifications.Application.Abstractions;
using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Preferences;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Notifications.Application.Preferences;

/// <summary>
/// NTF-5's category-level opt-out mechanism. See <see cref="RecipientNotificationPreference"/>'s own
/// remarks for why this has no public HTTP endpoint in this first-pass build (requirement-spec.md
/// §6's API surface table does not list a self-service preferences endpoint) - exercised directly
/// by unit/integration tests and by the dispatch pipeline's own send-time check.
/// </summary>
public sealed class RecipientPreferenceService(IRecipientPreferenceRepository preferences, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<Result> SetOptOutAsync(Guid recipientId, NotificationCategory category, bool optedOut, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var existing = await preferences.GetAsync(recipientId, category, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            if (optedOut && category.IsMandatory())
            {
                return Result.Failure(Error.Validation("notification_preference.mandatory_category", $"Category '{category}' is mandatory and cannot be opted out of."));
            }

            existing.SetOptedOut(optedOut, now);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }

        if (!optedOut)
        {
            // Absence of a row already means "not opted out" (default) - nothing to persist.
            return Result.Success();
        }

        var optOutResult = RecipientNotificationPreference.OptOut(recipientId, category, now);
        if (optOutResult.IsFailure)
        {
            return Result.Failure(optOutResult.Error!);
        }

        preferences.Add(optOutResult.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public Task<bool> IsOptedOutAsync(Guid recipientId, NotificationCategory category, CancellationToken cancellationToken = default) =>
        preferences.IsOptedOutAsync(recipientId, category, cancellationToken);
}
