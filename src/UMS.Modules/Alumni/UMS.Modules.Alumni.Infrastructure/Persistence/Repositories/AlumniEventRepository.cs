using Microsoft.EntityFrameworkCore;
using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Domain.AlumniEvents;

namespace UMS.Modules.Alumni.Infrastructure.Persistence.Repositories;

internal sealed class AlumniEventRepository(AlumniDbContext context) : IAlumniEventRepository
{
    public Task<AlumniEvent?> GetByIdAsync(AlumniEventId id, CancellationToken cancellationToken = default) =>
        context.AlumniEvents.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public void Add(AlumniEvent alumniEvent) => context.AlumniEvents.Add(alumniEvent);
}

internal sealed class AlumniEventRsvpRepository(AlumniDbContext context) : IAlumniEventRsvpRepository
{
    public Task<AlumniEventRsvp?> GetAsync(AlumniEventId eventId, Guid alumnusId, CancellationToken cancellationToken = default) =>
        context.AlumniEventRsvps.FirstOrDefaultAsync(r => r.EventId == eventId && r.AlumnusId == alumnusId, cancellationToken);

    public async Task<IReadOnlyList<AlumniEventRsvp>> ListByEventAsync(AlumniEventId eventId, CancellationToken cancellationToken = default) =>
        await context.AlumniEventRsvps.Where(r => r.EventId == eventId).OrderByDescending(r => r.RespondedAt).ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(AlumniEventRsvp rsvp) => context.AlumniEventRsvps.Add(rsvp);
}
