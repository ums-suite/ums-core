using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Domain.Mentorship;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Alumni.Application.Mentorship;

/// <summary>ALM-11: <c>POST /alumni/mentorship/opt-in</c> (requirement-spec.md §2.5 first bullet).</summary>
public sealed class MentorshipOptInService(IMentorshipOptInRepository optIns, IUnitOfWork unitOfWork, IClock clock)
{
    public static MentorshipOptInDto ToDto(MentorshipOptIn optIn) => new(
        optIn.Id.Value, optIn.PersonId, optIn.Role.ToString(), optIn.ExpertiseAreas, optIn.CapacityLimit, optIn.ActiveCount, optIn.Availability, optIn.IsActive, optIn.CreatedAt);

    public async Task<Result<MentorshipOptInDto>> OptInAsync(Guid personId, OptInRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<MentorshipRole>(request.Role, ignoreCase: true, out var role))
        {
            return Error.Validation("mentorshipoptin.invalid_role", $"'{request.Role}' is not a recognized MentorshipRole.");
        }

        var existing = await optIns.GetAsync(personId, role, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return ToDto(existing);
        }

        MentorshipOptIn optIn;
        try
        {
            optIn = MentorshipOptIn.OptIn(personId, role, request.ExpertiseAreas, request.CapacityLimit, request.Availability, clock.UtcNow);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Error.Validation("mentorshipoptin.invalid", ex.Message);
        }

        optIns.Add(optIn);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DuplicateValueException)
        {
            var reloaded = await optIns.GetAsync(personId, role, cancellationToken).ConfigureAwait(false);
            return reloaded is not null ? ToDto(reloaded) : Error.Failure("mentorshipoptin.create_race_unresolved", "Unique-violation on (PersonId, Role) but no row is now readable.");
        }

        return ToDto(optIn);
    }

    public async Task<Result<MentorshipOptInDto>> GetAsync(Guid personId, string role, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<MentorshipRole>(role, ignoreCase: true, out var parsedRole))
        {
            return Error.Validation("mentorshipoptin.invalid_role", $"'{role}' is not a recognized MentorshipRole.");
        }

        var optIn = await optIns.GetAsync(personId, parsedRole, cancellationToken).ConfigureAwait(false);
        return optIn is null ? Error.NotFound("mentorshipoptin.not_found", $"No MentorshipOptIn exists for person '{personId}' as '{role}'.") : ToDto(optIn);
    }
}
