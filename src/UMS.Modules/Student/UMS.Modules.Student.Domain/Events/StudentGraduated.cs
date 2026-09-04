using UMS.Modules.Student.Domain.Common;

namespace UMS.Modules.Student.Domain.Events;

/// <summary>requirement-spec.md student §3: primary consumer is Alumni (glossary: "the identity a graduated Student becomes"), once Alumni (Flow #29) exists.</summary>
public sealed record StudentGraduated(Guid StudentId, DateTimeOffset OccurredAt) : IDomainEvent;
