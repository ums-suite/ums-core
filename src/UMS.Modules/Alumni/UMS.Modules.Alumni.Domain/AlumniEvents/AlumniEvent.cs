using UMS.Modules.Alumni.Domain.Common;

namespace UMS.Modules.Alumni.Domain.AlumniEvents;

/// <summary>
/// ALM-14: a lightweight Alumni-owned event record (requirement-spec.md §2.7 - "modeled here as a
/// lightweight Alumni-owned entity that references Content's Event for shared calendar/publication
/// plumbing where applicable, rather than duplicating Content's full publish lifecycle").
///
/// <para>
/// <b>Scope note:</b> requirement-spec.md §6's API table lists only <c>POST /events/{id}/rsvp</c> -
/// event CREATION is not itself a named ticket/endpoint. A minimal creation path is added here
/// (gated by <c>alumni.chapter.manage</c>, reusing the same Admin/chapter-lead capability rather than
/// inventing a new Permission for a one-field creation the spec never separately names) purely so
/// there is something real to RSVP against; this is a reasonable minimal extension, not a
/// reinterpretation of the ticket's own scope.
/// </para>
/// </summary>
public sealed class AlumniEvent : AggregateRoot<AlumniEventId>
{
    private AlumniEvent()
    {
    }

    private AlumniEvent(AlumniEventId id, string title, string? description, Guid? chapterId, Guid? contentEventId, DateTimeOffset eventDate, DateTimeOffset now)
    {
        Id = id;
        Title = title;
        Description = description;
        ChapterId = chapterId;
        ContentEventId = contentEventId;
        EventDate = eventDate;
        CreatedAt = now;
    }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>Optional chapter-scoping (requirement-spec.md §2.6 last bullet: "Chapters are referenced by ... Events for chapter-scoped events").</summary>
    public Guid? ChapterId { get; private set; }

    /// <summary>Optional cross-reference to Content's own university-wide Event id (requirement-spec.md §2.7) - never required, Alumni's own lifecycle does not depend on Content's.</summary>
    public Guid? ContentEventId { get; private set; }

    public DateTimeOffset EventDate { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static AlumniEvent Create(string title, string? description, Guid? chapterId, Guid? contentEventId, DateTimeOffset eventDate, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("An AlumniEvent's title is required.", nameof(title));
        }

        return new AlumniEvent(AlumniEventId.New(), title.Trim(), description?.Trim(), chapterId, contentEventId, eventDate, now);
    }
}
