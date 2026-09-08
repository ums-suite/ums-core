namespace UMS.Modules.Content.Domain.Common;

/// <summary>
/// requirement-spec.md §2.1/§2.2: the one shared audience-targeting vocabulary for both
/// <see cref="Notices.Notice"/> and <see cref="Events.Event"/> ("the same audience-scoping model as
/// Notice") - a bit-flags enum since a Notice/Event may target more than one audience at once
/// (e.g. both Student and Faculty). Stored as a plain <see cref="int"/> column.
/// </summary>
[Flags]
public enum ContentAudience
{
    None = 0,
    Public = 1,
    Student = 2,
    Faculty = 4,
    Admin = 8,
}
