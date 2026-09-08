namespace UMS.Modules.Alumni.Domain.AlumniEvents;

/// <summary>
/// ALM-14: an Alumnus's RSVP against an <see cref="AlumniEvent"/> (requirement-spec.md §2.7, §3 -
/// "Module-Local Term, pending glossary merge"). One row per (EventId, AlumnusId) - a repeat RSVP
/// updates the existing row rather than creating a duplicate.
/// </summary>
public sealed class AlumniEventRsvp
{
    private AlumniEventRsvp()
    {
    }

    private AlumniEventRsvp(Guid id, AlumniEventId eventId, Guid alumnusId, RsvpResponse response, int guestCount, DateTimeOffset respondedAt)
    {
        Id = id;
        EventId = eventId;
        AlumnusId = alumnusId;
        Response = response;
        GuestCount = guestCount;
        RespondedAt = respondedAt;
    }

    public Guid Id { get; private set; }

    public AlumniEventId EventId { get; private set; }

    public Guid AlumnusId { get; private set; }

    public RsvpResponse Response { get; private set; }

    public int GuestCount { get; private set; }

    public DateTimeOffset RespondedAt { get; private set; }

    public static AlumniEventRsvp Create(AlumniEventId eventId, Guid alumnusId, RsvpResponse response, int guestCount, DateTimeOffset now)
    {
        if (guestCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(guestCount), "Guest count cannot be negative.");
        }

        return new AlumniEventRsvp(Guid.NewGuid(), eventId, alumnusId, response, guestCount, now);
    }

    public void Update(RsvpResponse response, int guestCount, DateTimeOffset now)
    {
        if (guestCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(guestCount), "Guest count cannot be negative.");
        }

        Response = response;
        GuestCount = guestCount;
        RespondedAt = now;
    }
}
