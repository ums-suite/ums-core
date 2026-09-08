using UMS.Modules.Career.Application.Abstractions;
using UMS.Modules.Career.Domain.Internships;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Career.Application.Internships;

/// <summary>CAR-2/CAR-4: Internship posting lifecycle and browse (requirement-spec.md §2.2, §3, §4).</summary>
public sealed class InternshipService(IInternshipRepository internships, IUnitOfWork unitOfWork, IClock clock)
{
    public static InternshipDto ToDto(Internship internship) => new(
        internship.Id.Value,
        internship.EmployerProfileId,
        internship.Title,
        internship.Description,
        internship.Location,
        internship.StipendNote,
        internship.ApplicationDeadline,
        internship.Status.ToString(),
        internship.WithdrawalReason,
        new EligibilityCriteriaDto(internship.EligibilityProgramIds, internship.EligibilityMinCgpa, internship.EligibilityMinYearOfStudy),
        internship.CreatedAt,
        internship.PublishedAt,
        internship.Version);

    public async Task<Result<InternshipDto>> CreateAsync(CreateInternshipRequest request, CancellationToken cancellationToken = default)
    {
        Internship internship;
        try
        {
            internship = Internship.Create(
                request.EmployerProfileId,
                request.Title,
                request.Description,
                request.Location,
                request.StipendNote,
                request.ApplicationDeadline,
                new EligibilityCriteria(request.Eligibility.ProgramIds, request.Eligibility.MinCgpa, request.Eligibility.MinYearOfStudy),
                clock.UtcNow);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("internship.invalid", ex.Message);
        }

        internships.Add(internship);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(internship);
    }

    public async Task<Result<InternshipDto>> EditAsync(Guid id, EditInternshipRequest request, CancellationToken cancellationToken = default)
    {
        var internship = await internships.GetByIdAsync(new InternshipId(id), cancellationToken).ConfigureAwait(false);
        if (internship is null)
        {
            return Error.NotFound("internship.not_found", $"No Internship exists with id '{id}'.");
        }

        try
        {
            internship.Edit(
                request.Title,
                request.Description,
                request.Location,
                request.StipendNote,
                request.ApplicationDeadline,
                new EligibilityCriteria(request.Eligibility.ProgramIds, request.Eligibility.MinCgpa, request.Eligibility.MinYearOfStudy),
                clock.UtcNow);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("internship.invalid", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Conflict("internship.invalid_transition", ex.Message);
        }

        unitOfWork.SetExpectedVersion(internship, request.Version);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Error.Conflict("internship.concurrency_conflict", ex.Message);
        }

        return ToDto(internship);
    }

    public async Task<Result<InternshipDto>> PublishAsync(Guid id, uint version, CancellationToken cancellationToken = default) =>
        await TransitionAsync(id, version, i => i.Publish(clock.UtcNow), cancellationToken).ConfigureAwait(false);

    public async Task<Result<InternshipDto>> OpenApplicationsAsync(Guid id, uint version, CancellationToken cancellationToken = default) =>
        await TransitionAsync(id, version, i => i.OpenApplications(), cancellationToken).ConfigureAwait(false);

    public async Task<Result<InternshipDto>> CloseApplicationsAsync(Guid id, uint version, CancellationToken cancellationToken = default) =>
        await TransitionAsync(id, version, i => i.CloseApplications(clock.UtcNow), cancellationToken).ConfigureAwait(false);

    /// <summary>CAR-9: withdrawal itself - the cascade to affected `CareerApplication`s is handled by `InternshipWithdrawalCascadeHandler`, invoked by the caller (Api layer) AFTER this commits, per design-decisions.md's in-process-event posture (the posting's own write commits first).</summary>
    public async Task<Result<InternshipDto>> WithdrawAsync(Guid id, WithdrawInternshipRequest request, CancellationToken cancellationToken = default) =>
        await TransitionAsync(id, request.Version, i => i.Withdraw(request.Reason), cancellationToken).ConfigureAwait(false);

    public async Task<Result<InternshipDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var internship = await internships.GetByIdAsync(new InternshipId(id), cancellationToken).ConfigureAwait(false);
        return internship is null ? Error.NotFound("internship.not_found", $"No Internship exists with id '{id}'.") : ToDto(internship);
    }

    /// <summary>CAR-4: browse/search - only Published/ApplicationsOpen postings for the Student-facing view (requirement-spec.md §2.2).</summary>
    public async Task<IReadOnlyList<InternshipDto>> ListAsync(Guid? programId, Guid? employerProfileId, string? keyword, bool includeAllStatuses, int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take <= 0 ? 50 : take, 1, 200);
        var items = await internships.ListAsync(programId, employerProfileId, keyword, includeAllStatuses, skip, take, cancellationToken).ConfigureAwait(false);
        return items.Select(ToDto).ToList();
    }

    private async Task<Result<InternshipDto>> TransitionAsync(Guid id, uint version, Action<Internship> transition, CancellationToken cancellationToken)
    {
        var internship = await internships.GetByIdAsync(new InternshipId(id), cancellationToken).ConfigureAwait(false);
        if (internship is null)
        {
            return Error.NotFound("internship.not_found", $"No Internship exists with id '{id}'.");
        }

        try
        {
            transition(internship);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("internship.invalid", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Conflict("internship.invalid_transition", ex.Message);
        }

        unitOfWork.SetExpectedVersion(internship, version);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Error.Conflict("internship.concurrency_conflict", ex.Message);
        }

        return ToDto(internship);
    }
}
