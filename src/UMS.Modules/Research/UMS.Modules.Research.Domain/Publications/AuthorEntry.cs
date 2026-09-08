namespace UMS.Modules.Research.Domain.Publications;

/// <summary>
/// design-decisions.md "Mixed Internal/External Authorship Modeling": ONE discriminated ordered
/// list, never two separate lists. <see cref="Order"/> is explicit and persisted - never inferred
/// from array/list position (requirement-spec.md §4) - so an individual author edit can never
/// silently reorder the remaining authors. <see cref="FacultyMemberId"/> is populated only for a
/// genuine internal author; <see cref="Name"/>/<see cref="Affiliation"/> are always present so a
/// citation string can be rendered by iterating in <see cref="Order"/> with one rendering path
/// regardless of internal/external status (edge-cases.md).
/// </summary>
public sealed record AuthorEntry(int Order, Guid? FacultyMemberId, string Name, string? Affiliation, bool IsCorrespondingAuthor);
