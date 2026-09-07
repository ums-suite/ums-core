using UMS.Modules.Student.Domain.Common;

namespace UMS.Modules.Student.Domain.Events;

/// <summary>requirement-spec.md student §3: "Document/action delivered" - STU-13's own fulfillment step. <paramref name="GeneratedDocumentId"/> is <see langword="null"/> for a Grievance (no document deliverable - the decision itself is the fulfillment).</summary>
public sealed record StudentRequestFulfilled(Guid StudentRequestId, Guid StudentId, Guid? GeneratedDocumentId, DateTimeOffset OccurredAt) : IDomainEvent;
