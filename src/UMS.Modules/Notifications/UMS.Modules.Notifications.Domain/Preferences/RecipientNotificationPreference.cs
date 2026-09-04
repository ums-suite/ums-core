using UMS.Modules.Notifications.Domain.Common;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Notifications.Domain.Preferences;

/// <summary>
/// requirement-spec.md §9 Decision 3: "a category-level opt-out model, not an all-or-nothing channel
/// toggle". One row per (<see cref="RecipientId"/>, <see cref="Category"/>) - absence of a row means
/// "not opted out" (the default), matching how a recipient who has never touched their notification
/// settings should behave.
///
/// <para>
/// No public HTTP endpoint manages this today - requirement-spec.md §6's own API surface table does
/// not list one (self-service preference management is out of this first-pass spec's literal scope,
/// same footing as the full template catalog being deferred per §9 Open Questions). This aggregate
/// and <c>Application.Preferences.RecipientPreferenceService</c> are still fully real and exercised
/// directly by unit/integration tests and the dispatch pipeline's send-time check (NTF-5) - only the
/// self-service HTTP surface is the (documented) gap, not the mechanism itself.
/// </para>
/// </summary>
public sealed class RecipientNotificationPreference : AggregateRoot<RecipientNotificationPreferenceId>
{
    private RecipientNotificationPreference()
    {
    }

    public Guid RecipientId { get; private set; }

    public NotificationCategory Category { get; private set; }

    public bool OptedOut { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>§9 Decision 3: a mandatory category (everything except <see cref="NotificationCategory.Informational"/>) can never be opted out of - enforced here, on the aggregate, not by an external caller trusting itself to check first.</summary>
    public static Result<RecipientNotificationPreference> OptOut(Guid recipientId, NotificationCategory category, DateTimeOffset now)
    {
        if (category.IsMandatory())
        {
            return Error.Validation("notification_preference.mandatory_category", $"Category '{category}' is mandatory and cannot be opted out of.");
        }

        return new RecipientNotificationPreference
        {
            Id = RecipientNotificationPreferenceId.New(),
            RecipientId = recipientId,
            Category = category,
            OptedOut = true,
            UpdatedAt = now,
        };
    }

    public void SetOptedOut(bool optedOut, DateTimeOffset now)
    {
        OptedOut = optedOut;
        UpdatedAt = now;
    }
}
