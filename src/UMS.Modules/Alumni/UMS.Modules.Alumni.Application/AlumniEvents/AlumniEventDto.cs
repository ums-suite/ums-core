namespace UMS.Modules.Alumni.Application.AlumniEvents;

public sealed record AlumniEventDto(Guid Id, string Title, string? Description, Guid? ChapterId, Guid? ContentEventId, DateTimeOffset EventDate, DateTimeOffset CreatedAt);

public sealed record CreateAlumniEventRequest(string Title, string? Description, Guid? ChapterId, Guid? ContentEventId, DateTimeOffset EventDate);

public sealed record RsvpDto(Guid Id, Guid EventId, Guid AlumnusId, string Response, int GuestCount, DateTimeOffset RespondedAt);

public sealed record SubmitRsvpRequest(string Response, int GuestCount);
