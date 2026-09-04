namespace UMS.Modules.Notifications.Domain.Requests;

/// <summary>
/// design-decisions.md, "Retry/Backoff Design Per Channel": an explicit <see cref="InFlight"/> state
/// is what lets the retry scheduler tell "merely slow" apart from "actually dead" (edge-cases.md, "A
/// retry racing a not-yet-failed original delivery attempt") - see
/// <see cref="Requests.NotificationDeliveryAttempt"/>'s own remarks for the full state machine.
/// </summary>
public enum DeliveryAttemptStatus
{
    Pending = 0,
    InFlight = 1,
    Retrying = 2,
    Delivered = 3,

    /// <summary>Suppressed by a category-level opt-out (§9 Decision 3) - a successful no-op, not a failure (see <see cref="DeadLettered"/> for the distinct failure-terminal states).</summary>
    Suppressed = 4,

    /// <summary>Terminal failure - see <see cref="Requests.DeadLetterReason"/> for why.</summary>
    DeadLettered = 5,
}

/// <summary>
/// requirement-spec.md §8 edge cases: "fails fast with a distinguishable NoContactInfo status" and
/// "dead-lettered immediately with a distinguishable TemplateMissing reason" - both immediate,
/// zero-retry terminal outcomes, alongside the ordinary exhausted-retry-budget path.
/// </summary>
public enum DeadLetterReason
{
    RetriesExhausted = 0,
    NoContactInfo = 1,
    TemplateMissing = 2,
    ProviderRejected = 3,
}
