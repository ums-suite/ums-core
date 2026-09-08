using UMS.Modules.Alumni.Domain.AlumniEvents;

namespace UMS.Modules.Alumni.Application.Abstractions;

public interface IAlumniEventRepository
{
    public Task<AlumniEvent?> GetByIdAsync(AlumniEventId id, CancellationToken cancellationToken = default);

    public void Add(AlumniEvent alumniEvent);
}

public interface IAlumniEventRsvpRepository
{
    public Task<AlumniEventRsvp?> GetAsync(AlumniEventId eventId, Guid alumnusId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<AlumniEventRsvp>> ListByEventAsync(AlumniEventId eventId, CancellationToken cancellationToken = default);

    public void Add(AlumniEventRsvp rsvp);
}
