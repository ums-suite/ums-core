using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Domain.AlumniEvents;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Alumni.Application.AlumniEvents;

/// <summary>ALM-14's supporting event record - see <see cref="Domain.AlumniEvents.AlumniEvent"/>'s own remarks on why a minimal creation path is included here.</summary>
public sealed class AlumniEventService(IAlumniEventRepository events, IUnitOfWork unitOfWork, IClock clock)
{
    public static AlumniEventDto ToDto(Domain.AlumniEvents.AlumniEvent alumniEvent) => new(
        alumniEvent.Id.Value, alumniEvent.Title, alumniEvent.Description, alumniEvent.ChapterId, alumniEvent.ContentEventId, alumniEvent.EventDate, alumniEvent.CreatedAt);

    public async Task<Result<AlumniEventDto>> CreateAsync(CreateAlumniEventRequest request, CancellationToken cancellationToken = default)
    {
        Domain.AlumniEvents.AlumniEvent alumniEvent;
        try
        {
            alumniEvent = Domain.AlumniEvents.AlumniEvent.Create(request.Title, request.Description, request.ChapterId, request.ContentEventId, request.EventDate, clock.UtcNow);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("alumnievent.invalid", ex.Message);
        }

        events.Add(alumniEvent);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(alumniEvent);
    }

    public async Task<Result<AlumniEventDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var alumniEvent = await events.GetByIdAsync(new AlumniEventId(id), cancellationToken).ConfigureAwait(false);
        return alumniEvent is null ? Error.NotFound("alumnievent.not_found", $"No AlumniEvent exists with id '{id}'.") : ToDto(alumniEvent);
    }
}
