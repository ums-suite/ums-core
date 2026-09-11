using UMS.Modules.Career.Application.Abstractions;
using UMS.Modules.Career.Domain.Employers;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Career.Application.Employers;

/// <summary>
/// CAR-1: staff-curated CRUD (requirement-spec.md §2.1) - no employer-facing login exists
/// (design-decisions.md "Employer Identity Model"), so every write here is invoked only by
/// Career-Services-staff/Admin (gated at the Api layer by `career.employer.manage`).
/// </summary>
public sealed class EmployerProfileService(IEmployerProfileRepository employers, IUnitOfWork unitOfWork, IClock clock)
{
    public static EmployerProfileDto ToDto(EmployerProfile profile) => new(
        profile.Id.Value,
        profile.CompanyName,
        profile.Industry,
        profile.Website,
        profile.ContactName,
        profile.ContactEmail,
        profile.ContactPhone,
        profile.VerificationNote,
        profile.IsArchived,
        profile.CreatedAt,
        profile.Version);

    public async Task<Result<EmployerProfileDto>> CreateAsync(CreateEmployerProfileRequest request, CancellationToken cancellationToken = default)
    {
        EmployerProfile profile;
        try
        {
            profile = EmployerProfile.Create(request.CompanyName, request.Industry, request.Website, request.ContactName, request.ContactEmail, request.ContactPhone, request.VerificationNote, clock.UtcNow);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("employerprofile.invalid", ex.Message);
        }

        employers.Add(profile);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(profile);
    }

    public async Task<Result<EmployerProfileDto>> UpdateAsync(Guid id, UpdateEmployerProfileRequest request, CancellationToken cancellationToken = default)
    {
        var profile = await employers.GetByIdAsync(new EmployerProfileId(id), cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return Error.NotFound("employerprofile.not_found", $"No EmployerProfile exists with id '{id}'.");
        }

        try
        {
            profile.Update(request.CompanyName, request.Industry, request.Website, request.ContactName, request.ContactEmail, request.ContactPhone, request.VerificationNote);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("employerprofile.invalid", ex.Message);
        }

        unitOfWork.SetExpectedVersion(profile, request.Version);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Error.Conflict("employerprofile.concurrency_conflict", ex.Message);
        }

        return ToDto(profile);
    }

    public async Task<Result<EmployerProfileDto>> ArchiveAsync(Guid id, uint version, bool archive, CancellationToken cancellationToken = default)
    {
        var profile = await employers.GetByIdAsync(new EmployerProfileId(id), cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return Error.NotFound("employerprofile.not_found", $"No EmployerProfile exists with id '{id}'.");
        }

        if (archive)
        {
            profile.Archive();
        }
        else
        {
            profile.Unarchive();
        }

        unitOfWork.SetExpectedVersion(profile, version);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Error.Conflict("employerprofile.concurrency_conflict", ex.Message);
        }

        return ToDto(profile);
    }

    public async Task<Result<EmployerProfileDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var profile = await employers.GetByIdAsync(new EmployerProfileId(id), cancellationToken).ConfigureAwait(false);
        return profile is null ? Error.NotFound("employerprofile.not_found", $"No EmployerProfile exists with id '{id}'.") : ToDto(profile);
    }

    public async Task<IReadOnlyList<EmployerProfileDto>> ListAsync(bool includeArchived, int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take <= 0 ? 50 : take, 1, 200);
        var items = await employers.ListAsync(includeArchived, skip, take, cancellationToken).ConfigureAwait(false);
        return items.Select(ToDto).ToList();
    }
}
