using UMS.Modules.Faculty.Domain.Common;
using UMS.Modules.Faculty.Domain.Events;

namespace UMS.Modules.Faculty.Domain.ResearchProfiles;

/// <summary>FAC-13: one per FacultyMember (requirement-spec.md faculty §2 Research Profile, §3). Publicly surfaced via Content/Public-Website's own read path against Faculty's public query interface - Faculty never writes into Content's schema (§2).</summary>
public sealed class ResearchProfile : AggregateRoot<ResearchProfileId>
{
    private readonly List<Publication> _publications = [];

    private ResearchProfile()
    {
    }

    private ResearchProfile(ResearchProfileId id, Guid facultyMemberId, DateTimeOffset now)
    {
        Id = id;
        FacultyMemberId = facultyMemberId;
        CreatedAt = now;
    }

    public Guid FacultyMemberId { get; private set; }

    public IReadOnlyCollection<Publication> Publications => _publications.AsReadOnly();

    public string? OngoingResearch { get; private set; }

    public string? Grants { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static ResearchProfile Create(Guid facultyMemberId, DateTimeOffset now) => new(ResearchProfileId.New(), facultyMemberId, now);

    public void Update(IReadOnlyCollection<Publication> publications, string? ongoingResearch, string? grants, DateTimeOffset now)
    {
        _publications.Clear();
        _publications.AddRange(publications);
        OngoingResearch = string.IsNullOrWhiteSpace(ongoingResearch) ? null : ongoingResearch.Trim();
        Grants = string.IsNullOrWhiteSpace(grants) ? null : grants.Trim();
        Raise(new ResearchProfileUpdated(Id.Value, FacultyMemberId, now));
    }
}
