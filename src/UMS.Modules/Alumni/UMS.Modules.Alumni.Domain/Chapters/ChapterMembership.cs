using UMS.Modules.Alumni.Domain.Alumni;

namespace UMS.Modules.Alumni.Domain.Chapters;

/// <summary>requirement-spec.md §2.6: "membership is a join, not a separate aggregate" - an owned child of <see cref="AlumniChapter"/>.</summary>
public sealed class ChapterMembership
{
    internal ChapterMembership(AlumnusId alumnusId, DateTimeOffset joinedAt)
    {
        AlumnusId = alumnusId;
        JoinedAt = joinedAt;
    }

    private ChapterMembership()
    {
    }

    public AlumnusId AlumnusId { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }
}
