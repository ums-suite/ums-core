using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Events;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Notifications.Domain.Requests;

/// <summary>
/// requirement-spec.md §3: "One fan-out request (event + recipient + channel set) accepted from
/// another module". The aggregate root for NTF-3/4/5/6 - creation is where the fan-out invariant
/// ("fan out one NotificationRequest into one or more NotificationDeliveryAttempts, one per
/// requested channel", §2) is enforced, so a <see cref="NotificationRequest"/> is never observed
/// without its full set of per-channel attempts already present.
///
/// <para>
/// <b>Deduplication</b> (§4 invariant; §9 Decision 2; edge-cases.md "Two modules ... raising the
/// same logical NotificationRequest concurrently"): the natural key
/// (<see cref="SourceModule"/>, <see cref="EventType"/>, <see cref="SourceEntityId"/>,
/// <see cref="RecipientId"/>) is enforced as a genuine database unique constraint, not just an
/// application-level check - <see cref="Application.Requests.SubmitNotificationRequestService"/> is
/// what attempts the insert and treats a constraint violation as "already accepted", per design-
/// decisions.md's "Dedup-Key Enforcement Mechanism" decision. This aggregate itself does not (and
/// cannot) re-implement that guarantee - a factory method has no visibility into concurrent inserts
/// from other transactions.
/// </para>
/// </summary>
public sealed class NotificationRequest : AggregateRoot<NotificationRequestId>
{
    private readonly List<NotificationDeliveryAttempt> _attempts = [];

    private NotificationRequest()
    {
    }

    public string SourceModule { get; private set; } = string.Empty;

    public string EventType { get; private set; } = string.Empty;

    public string SourceEntityId { get; private set; } = string.Empty;

    public Guid RecipientId { get; private set; }

    public NotificationCategory Category { get; private set; }

    public NotificationPriority Priority { get; private set; }

    /// <summary>Publishing-module-supplied override of the recipient's resolved language for this one request - see <c>UMS.Shared.Identity.RecipientContactInfo</c>'s remarks for why this exists.</summary>
    public string? LanguageOverride { get; private set; }

    /// <summary>Merge-field data (JSON) the resolved <see cref="Templates.Template"/> is rendered against - e.g. <c>{"amount":"1200","invoiceNo":"INV-1"}</c>.</summary>
    public string PayloadJson { get; private set; } = "{}";

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<NotificationDeliveryAttempt> Attempts => _attempts.AsReadOnly();

    public static Result<NotificationRequest> Create(
        string sourceModule,
        string eventType,
        string sourceEntityId,
        Guid recipientId,
        NotificationCategory category,
        NotificationPriority priority,
        IReadOnlyCollection<NotificationChannel> requestedChannels,
        string payloadJson,
        string? languageOverride,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(sourceModule))
        {
            return Error.Validation("notification_request.source_module_required", "Source module is required.");
        }

        if (string.IsNullOrWhiteSpace(eventType))
        {
            return Error.Validation("notification_request.event_type_required", "Event type is required.");
        }

        if (string.IsNullOrWhiteSpace(sourceEntityId))
        {
            return Error.Validation("notification_request.source_entity_id_required", "Source entity id is required.");
        }

        if (requestedChannels.Count == 0)
        {
            return Error.Validation("notification_request.no_channels", "At least one channel must be requested.");
        }

        var request = new NotificationRequest
        {
            Id = NotificationRequestId.New(),
            SourceModule = sourceModule.Trim(),
            EventType = eventType.Trim(),
            SourceEntityId = sourceEntityId.Trim(),
            RecipientId = recipientId,
            Category = category,
            Priority = priority,
            LanguageOverride = string.IsNullOrWhiteSpace(languageOverride) ? null : languageOverride.Trim(),
            PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
            CreatedAt = now,
        };

        // §4 invariant "In-app is treated as Notifications' own 'must succeed' channel - its record
        // is always created regardless of email/SMS/push outcome" - satisfied here by construction:
        // InApp gets exactly the same unconditional attempt-creation as every other requested
        // channel; it is only ever skipped by the dispatch pipeline for opt-out (§9 Decision 3's
        // "not exempt from a category-level opt-out"), never dropped at fan-out time.
        foreach (var channel in requestedChannels.Distinct())
        {
            request._attempts.Add(NotificationDeliveryAttempt.CreatePending(request.Id, channel, now));
        }

        request.Raise(new NotificationRequestReceived(request.Id.Value, request.SourceModule, request.EventType, request.RecipientId, request.Category, now));

        return request;
    }
}
