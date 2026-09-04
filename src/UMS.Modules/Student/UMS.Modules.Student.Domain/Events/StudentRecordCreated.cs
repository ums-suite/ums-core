using UMS.Modules.Student.Domain.Common;

namespace UMS.Modules.Student.Domain.Events;

/// <summary>requirement-spec.md student §3: "CreateStudentRecord command completes" - raised ONLY on the first, successful insert branch, never on the caught-constraint-violation idempotent-return branch (design-decisions.md).</summary>
public sealed record StudentRecordCreated(Guid StudentId, Guid OriginatingApplicationId, string StudentNumber, DateTimeOffset OccurredAt) : IDomainEvent;
