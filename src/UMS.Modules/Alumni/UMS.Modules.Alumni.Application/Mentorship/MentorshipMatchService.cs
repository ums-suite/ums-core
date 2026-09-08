using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Domain.Mentorship;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Alumni.Application.Mentorship;

/// <summary>
/// ALM-12/ALM-13: mentorship match proposal, two-sided acceptance, and lifecycle (requirement-spec.md
/// §2.5, §3, §4). design-decisions.md "Mentor-Capacity Enforcement Mechanism": capacity is claimed
/// BEFORE a match is even constructed - a proposal only proceeds if
/// <see cref="IMentorshipOptInRepository.TryClaimMentorCapacityAsync"/>'s atomic conditional write
/// actually affected a row, and released again on <see cref="RejectAsync"/>/<see cref="EndAsync"/>.
/// </summary>
public sealed class MentorshipMatchService(IMentorshipMatchRepository matches, IMentorshipOptInRepository optIns, IUnitOfWork unitOfWork, IClock clock)
{
    public static MentorshipMatchDto ToDto(MentorshipMatch match) => new(
        match.Id.Value, match.MentorAlumnusId, match.MenteeStudentId, match.Status.ToString(), match.ProposedAt, match.MentorAcceptedAt, match.MenteeAcceptedAt, match.ActivatedAt, match.EndedAt, match.EndedReason);

    /// <summary>edge-cases.md "Concurrent MentorshipMatch requests for the same limited-capacity mentor": a coordinator's proposal that loses the race gets an immediate, synchronous rejection.</summary>
    public async Task<Result<MentorshipMatchDto>> ProposeAsync(ProposeMatchRequest request, CancellationToken cancellationToken = default)
    {
        var mentorOptIn = await optIns.GetAsync(request.MentorAlumnusId, MentorshipRole.Mentor, cancellationToken).ConfigureAwait(false);
        if (mentorOptIn is null || !mentorOptIn.IsActive)
        {
            return Error.NotFound("mentorshipmatch.mentor_not_opted_in", $"Alumnus '{request.MentorAlumnusId}' has not opted in as an active mentor.");
        }

        var menteeOptIn = await optIns.GetAsync(request.MenteeStudentId, MentorshipRole.Mentee, cancellationToken).ConfigureAwait(false);
        if (menteeOptIn is null || !menteeOptIn.IsActive)
        {
            return Error.NotFound("mentorshipmatch.mentee_not_opted_in", $"Student '{request.MenteeStudentId}' has not opted in as an active mentee.");
        }

        var claimed = await optIns.TryClaimMentorCapacityAsync(request.MentorAlumnusId, cancellationToken).ConfigureAwait(false);
        if (!claimed)
        {
            return Error.Conflict("mentorshipmatch.capacity_exceeded", $"Alumnus '{request.MentorAlumnusId}' has no remaining mentorship capacity.");
        }

        var match = MentorshipMatch.Propose(request.MentorAlumnusId, request.MenteeStudentId, clock.UtcNow);
        matches.Add(match);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Compensates the already-claimed slot - the conditional-write claim and the match's own
            // insert are two separate statements, so a failure between them must not leak a
            // permanently-claimed slot with no match to show for it.
            await optIns.ReleaseMentorCapacityAsync(request.MentorAlumnusId, cancellationToken).ConfigureAwait(false);
            throw;
        }

        return ToDto(match);
    }

    public async Task<Result<MentorshipMatchDto>> AcceptByMentorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var match = await matches.GetByIdAsync(new MentorshipMatchId(id), cancellationToken).ConfigureAwait(false);
        if (match is null)
        {
            return Error.NotFound("mentorshipmatch.not_found", $"No MentorshipMatch exists with id '{id}'.");
        }

        try
        {
            match.AcceptByMentor(clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Conflict("mentorshipmatch.invalid_transition", ex.Message);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(match);
    }

    public async Task<Result<MentorshipMatchDto>> AcceptByMenteeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var match = await matches.GetByIdAsync(new MentorshipMatchId(id), cancellationToken).ConfigureAwait(false);
        if (match is null)
        {
            return Error.NotFound("mentorshipmatch.not_found", $"No MentorshipMatch exists with id '{id}'.");
        }

        try
        {
            match.AcceptByMentee(clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Conflict("mentorshipmatch.invalid_transition", ex.Message);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(match);
    }

    /// <summary>A coordinator/Admin rejects a still-Proposed match - releases the mentor's claimed capacity slot.</summary>
    public async Task<Result<MentorshipMatchDto>> RejectAsync(Guid id, EndMatchRequest request, CancellationToken cancellationToken = default)
    {
        var match = await matches.GetByIdAsync(new MentorshipMatchId(id), cancellationToken).ConfigureAwait(false);
        if (match is null)
        {
            return Error.NotFound("mentorshipmatch.not_found", $"No MentorshipMatch exists with id '{id}'.");
        }

        try
        {
            match.Reject(request.Reason, clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Conflict("mentorshipmatch.invalid_transition", ex.Message);
        }

        await optIns.ReleaseMentorCapacityAsync(match.MentorAlumnusId, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(match);
    }

    /// <summary>edge-cases.md "A mentor withdraws mid-match": either side may end an Active match; releases the mentor's capacity so a new proposal can be made for the freed mentee (requirement-spec.md §8).</summary>
    public async Task<Result<MentorshipMatchDto>> EndAsync(Guid id, EndMatchRequest request, CancellationToken cancellationToken = default)
    {
        var match = await matches.GetByIdAsync(new MentorshipMatchId(id), cancellationToken).ConfigureAwait(false);
        if (match is null)
        {
            return Error.NotFound("mentorshipmatch.not_found", $"No MentorshipMatch exists with id '{id}'.");
        }

        try
        {
            match.End(request.Reason, clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Conflict("mentorshipmatch.invalid_transition", ex.Message);
        }

        await optIns.ReleaseMentorCapacityAsync(match.MentorAlumnusId, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(match);
    }

    public async Task<Result<MentorshipMatchDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var match = await matches.GetByIdAsync(new MentorshipMatchId(id), cancellationToken).ConfigureAwait(false);
        return match is null ? Error.NotFound("mentorshipmatch.not_found", $"No MentorshipMatch exists with id '{id}'.") : ToDto(match);
    }

    public async Task<IReadOnlyList<MentorshipMatchDto>> ListByMentorAsync(Guid mentorAlumnusId, CancellationToken cancellationToken = default) =>
        (await matches.ListByMentorAsync(mentorAlumnusId, cancellationToken).ConfigureAwait(false)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<MentorshipMatchDto>> ListByMenteeAsync(Guid menteeStudentId, CancellationToken cancellationToken = default) =>
        (await matches.ListByMenteeAsync(menteeStudentId, cancellationToken).ConfigureAwait(false)).Select(ToDto).ToList();
}
