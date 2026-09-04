namespace UMS.Modules.Student.Domain.Students;

/// <summary>
/// requirement-spec.md student §3 (glossary: "the append-only record of every Student status
/// transition and its reason"), §4 ("always a new history row, never an edit to a prior one").
/// design-decisions.md, "Append-Only StudentStatusHistory Write Pattern": inserted in the SAME
/// database transaction as the <see cref="Student.Status"/> write itself - both commit together or
/// not at all - as Student's own domain-owned academic-standing record, separate from (not a
/// substitute for) the synchronous Audit-module write ADR-0012 mandates for the same mutation.
/// The very first row (<see cref="FromStatus"/> <c>null</c>, <see cref="ToStatus"/>
/// <see cref="StudentStatus.Enrolled"/>) is appended at <see cref="Student.Enroll"/> time, giving
/// the history a complete trail from the Student's creation, not only from its first admin-driven
/// transition onward.
/// </summary>
public sealed class StudentStatusHistoryEntry
{
    private StudentStatusHistoryEntry()
    {
    }

    private StudentStatusHistoryEntry(StudentStatusHistoryEntryId id, StudentStatus? fromStatus, StudentStatus toStatus, string? reason, Guid? changedByUserId, DateTimeOffset changedAt)
    {
        Id = id;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Reason = reason;
        ChangedByUserId = changedByUserId;
        ChangedAt = changedAt;
    }

    public StudentStatusHistoryEntryId Id { get; private set; }

    /// <summary><c>null</c> only for the initial "created as Enrolled" row.</summary>
    public StudentStatus? FromStatus { get; private set; }

    public StudentStatus ToStatus { get; private set; }

    public string? Reason { get; private set; }

    /// <summary><c>null</c> for the system-originated initial-enrollment row; the acting admin/Registrar's Identity UserId for every subsequent transition.</summary>
    public Guid? ChangedByUserId { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    public static StudentStatusHistoryEntry Create(StudentStatus? fromStatus, StudentStatus toStatus, string? reason, Guid? changedByUserId, DateTimeOffset changedAt) =>
        new(StudentStatusHistoryEntryId.New(), fromStatus, toStatus, string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), changedByUserId, changedAt);
}
