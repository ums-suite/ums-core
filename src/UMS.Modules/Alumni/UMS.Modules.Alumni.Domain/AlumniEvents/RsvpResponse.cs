namespace UMS.Modules.Alumni.Domain.AlumniEvents;

/// <summary>requirement-spec.md §2.7: simple RSVP, no seating/capacity enforcement in v1.</summary>
public enum RsvpResponse
{
    Going,
    Interested,
    NotGoing,
}
