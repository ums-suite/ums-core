using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Domain.AlumniEvents;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Alumni.Application.AlumniEvents;

/// <summary>ALM-14: <c>POST /alumni/events/{id}/rsvp</c> (requirement-spec.md §2.7) - simple RSVP, no seating/capacity enforcement in v1; a repeat RSVP upserts the caller's own existing response.</summary>
public sealed class AlumniEventRsvpService(IAlumniEventRepository events, IAlumniEventRsvpRepository rsvps, IUnitOfWork unitOfWork, IClock clock)
{
    public static RsvpDto ToDto(AlumniEventRsvp rsvp) => new(rsvp.Id, rsvp.EventId.Value, rsvp.AlumnusId, rsvp.Response.ToString(), rsvp.GuestCount, rsvp.RespondedAt);

    public async Task<Result<RsvpDto>> SubmitAsync(Guid eventId, Guid alumnusId, SubmitRsvpRequest request, CancellationToken cancellationToken = default)
    {
        var alumniEvent = await events.GetByIdAsync(new AlumniEventId(eventId), cancellationToken).ConfigureAwait(false);
        if (alumniEvent is null)
        {
            return Error.NotFound("alumnievent.not_found", $"No AlumniEvent exists with id '{eventId}'.");
        }

        if (!Enum.TryParse<RsvpResponse>(request.Response, ignoreCase: true, out var response))
        {
            return Error.Validation("rsvp.invalid_response", $"'{request.Response}' is not a recognized RSVP response.");
        }

        var existing = await rsvps.GetAsync(new AlumniEventId(eventId), alumnusId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            try
            {
                existing.Update(response, request.GuestCount, clock.UtcNow);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Error.Validation("rsvp.invalid", ex.Message);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return ToDto(existing);
        }

        AlumniEventRsvp rsvp;
        try
        {
            rsvp = AlumniEventRsvp.Create(new AlumniEventId(eventId), alumnusId, response, request.GuestCount, clock.UtcNow);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Error.Validation("rsvp.invalid", ex.Message);
        }

        rsvps.Add(rsvp);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DuplicateValueException)
        {
            // A concurrently-submitted RSVP for the same (EventId, AlumnusId) already won - re-read
            // and update it instead of surfacing a raw constraint error to a double-click/retry.
            var reloaded = await rsvps.GetAsync(new AlumniEventId(eventId), alumnusId, cancellationToken).ConfigureAwait(false);
            if (reloaded is null)
            {
                return Error.Failure("rsvp.create_race_unresolved", "Unique-violation on (EventId, AlumnusId) but no row is now readable.");
            }

            reloaded.Update(response, request.GuestCount, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return ToDto(reloaded);
        }

        return ToDto(rsvp);
    }

    public async Task<IReadOnlyList<RsvpDto>> ListByEventAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        (await rsvps.ListByEventAsync(new AlumniEventId(eventId), cancellationToken).ConfigureAwait(false)).Select(ToDto).ToList();
}
