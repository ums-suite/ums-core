using UMS.Modules.Alumni.Domain.Alumni;
using UMS.Modules.Alumni.Domain.Common;

namespace UMS.Modules.Alumni.Domain.Chapters;

/// <summary>ALM-4: a regional/interest-based grouping (requirement-spec.md §2.6, §3). Admin/chapter-lead creates/manages; an Alumnus joins/leaves voluntarily.</summary>
public sealed class AlumniChapter : AggregateRoot<AlumniChapterId>
{
    private readonly List<ChapterMembership> _memberships = [];

    private AlumniChapter()
    {
    }

    private AlumniChapter(AlumniChapterId id, string name, string? description, string? region, DateTimeOffset now)
    {
        Id = id;
        Name = name;
        Description = description;
        Region = region;
        CreatedAt = now;
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string? Region { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<ChapterMembership> Memberships => _memberships.AsReadOnly();

    public static AlumniChapter Create(string name, string? description, string? region, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("An AlumniChapter's name is required.", nameof(name));
        }

        return new AlumniChapter(AlumniChapterId.New(), name.Trim(), description?.Trim(), region?.Trim(), now);
    }

    /// <summary>requirement-spec.md §2.6: an Alumnus joins voluntarily. Idempotent - joining twice is a no-op, not an error (mirrors the same "duplicate delivery/action is a no-op" posture ALM-1's own StudentGraduated consumption takes).</summary>
    public void Join(AlumnusId alumnusId, DateTimeOffset now)
    {
        if (_memberships.Any(m => m.AlumnusId == alumnusId))
        {
            return;
        }

        _memberships.Add(new ChapterMembership(alumnusId, now));
    }

    public void Leave(AlumnusId alumnusId) => _memberships.RemoveAll(m => m.AlumnusId == alumnusId);

    public bool HasMember(AlumnusId alumnusId) => _memberships.Any(m => m.AlumnusId == alumnusId);
}
