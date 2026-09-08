using System.Text.Json;
using UMS.Modules.Research.Application.Abstractions;
using UMS.Modules.Research.Application.Common;
using UMS.Modules.Research.Domain.InstitutionalRepositoryEntries;
using UMS.Shared.Audit;
using UMS.Shared.Documents;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Faculty;

namespace UMS.Modules.Research.Application.InstitutionalRepositoryEntries;

/// <summary>RES-10/RES-11: requirement-spec.md §2 Institutional Repository, §6 API Surface Repository rows.</summary>
public sealed class InstitutionalRepositoryEntryService(
    IInstitutionalRepositoryEntryRepository entries,
    IFacultyMemberLookup facultyMemberLookup,
    IUploadedArtifactRequester uploadedArtifactRequester,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    public static InstitutionalRepositoryEntryDto ToDto(InstitutionalRepositoryEntry entry) => new(
        entry.Id.Value,
        entry.Title,
        entry.WorkType.ToString(),
        new ContributorDto(entry.Depositor.Name, entry.Depositor.FacultyMemberId),
        entry.SupervisingFacultyMemberId,
        entry.DepositDate,
        new EmbargoPolicyDto(entry.Embargo.IsEmbargoed, entry.Embargo.EmbargoEndDate, entry.Embargo.AccessLevel.ToString()),
        entry.ArtifactId,
        entry.CreatedAt,
        entry.Version);

    public async Task<Result<InstitutionalRepositoryEntryDto>> DepositAsync(DepositRepositoryEntryRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<RepositoryWorkType>(request.WorkType, ignoreCase: true, out var workType))
        {
            return Error.Validation("repositoryentry.invalid_work_type", $"'{request.WorkType}' is not a recognized work type.");
        }

        if (!Enum.TryParse<RepositoryAccessLevel>(request.Embargo.AccessLevel, ignoreCase: true, out var accessLevel))
        {
            return Error.Validation("repositoryentry.invalid_access_level", $"'{request.Embargo.AccessLevel}' is not a recognized access level.");
        }

        // requirement-spec.md item 13: the depositor's FacultyMemberId (when present - a graduate
        // student depositor has none, §9) and the supervising advisor are both validated against
        // IFacultyMemberLookup at write time.
        if (request.Depositor.FacultyMemberId is { } depositorFacultyMemberId)
        {
            var depositorValidation = await ValidateFacultyMemberAsync(depositorFacultyMemberId, cancellationToken).ConfigureAwait(false);
            if (depositorValidation.IsFailure)
            {
                return depositorValidation.Error!;
            }
        }

        if (request.SupervisingFacultyMemberId is { } supervisorId)
        {
            var supervisorValidation = await ValidateFacultyMemberAsync(supervisorId, cancellationToken).ConfigureAwait(false);
            if (supervisorValidation.IsFailure)
            {
                return supervisorValidation.Error!;
            }
        }

        var embargoResult = EmbargoPolicy.Create(request.Embargo.IsEmbargoed, request.Embargo.EmbargoEndDate, accessLevel);
        if (embargoResult.IsFailure)
        {
            return embargoResult.Error!;
        }

        InstitutionalRepositoryEntry entry;
        try
        {
            entry = InstitutionalRepositoryEntry.Deposit(
                request.Title,
                workType,
                new Contributor(request.Depositor.Name, request.Depositor.FacultyMemberId),
                request.SupervisingFacultyMemberId,
                request.DepositDate,
                embargoResult.Value,
                clock.UtcNow);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("repositoryentry.invalid", ex.Message);
        }

        entries.Add(entry);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest("InstitutionalRepositoryEntry", entry.Id.Value.ToString(), AuditActions.Create, null, JsonSerializer.Serialize(new { title = entry.Title, isEmbargoed = entry.Embargo.IsEmbargoed }));
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : ToDto(entry);
    }

    /// <summary>UMS.Shared.Documents.IUploadedArtifactRequester's presigned-upload flow (mechanism #12) - requests an upload slot for the depositing user, storing only the returned artifactId once confirmed.</summary>
    public Task<Result<UploadedArtifactSlot>> RequestArtifactUploadAsync(Guid ownerId, string mimeType, CancellationToken cancellationToken = default) =>
        uploadedArtifactRequester.RequestUploadAsync(new RequestUploadedArtifactCommand(ownerId, "InstitutionalRepositoryEntryFile", mimeType), cancellationToken);

    public async Task<Result<InstitutionalRepositoryEntryDto>> ConfirmArtifactUploadAsync(Guid id, Guid artifactId, CancellationToken cancellationToken = default)
    {
        var entry = await entries.GetByIdAsync(new InstitutionalRepositoryEntryId(id), cancellationToken).ConfigureAwait(false);
        if (entry is null)
        {
            return Error.NotFound("repositoryentry.not_found", $"No InstitutionalRepositoryEntry exists with id '{id}'.");
        }

        var confirmed = await uploadedArtifactRequester.ConfirmAsync(artifactId, cancellationToken).ConfigureAwait(false);
        if (confirmed.IsFailure)
        {
            return confirmed.Error!;
        }

        entry.AttachArtifact(artifactId);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(entry);
    }

    /// <summary>RES-11: the explicit Admin early-override path - design-decisions.md's daily worker performs the automatic path via <c>EmbargoLiftService.LiftLapsedEmbargoesAsync</c> instead.</summary>
    public async Task<Result<InstitutionalRepositoryEntryDto>> LiftEmbargoAsync(Guid id, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var entry = await entries.GetByIdAsync(new InstitutionalRepositoryEntryId(id), cancellationToken).ConfigureAwait(false);
        if (entry is null)
        {
            return Error.NotFound("repositoryentry.not_found", $"No InstitutionalRepositoryEntry exists with id '{id}'.");
        }

        if (!entry.Embargo.IsEmbargoed)
        {
            return Error.Conflict("repositoryentry.not_embargoed", "This InstitutionalRepositoryEntry is not currently embargoed.");
        }

        entry.LiftEmbargo(clock.UtcNow);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest("InstitutionalRepositoryEntry", entry.Id.Value.ToString(), "lift_embargo", JsonSerializer.Serialize(new { isEmbargoed = true }), JsonSerializer.Serialize(new { isEmbargoed = false }));
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : ToDto(entry);
    }

    public async Task<Result<InstitutionalRepositoryEntryDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entry = await entries.GetByIdAsync(new InstitutionalRepositoryEntryId(id), cancellationToken).ConfigureAwait(false);
        return entry is null ? Error.NotFound("repositoryentry.not_found", $"No InstitutionalRepositoryEntry exists with id '{id}'.") : ToDto(entry);
    }

    public async Task<InstitutionalRepositoryEntryListPage> ListAsync(Guid? supervisingFacultyMemberId, int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take <= 0 ? 50 : take, 1, 200);

        var items = await entries.ListAsync(supervisingFacultyMemberId, skip, take, cancellationToken).ConfigureAwait(false);
        return new InstitutionalRepositoryEntryListPage(items.Select(ToDto).ToList(), skip, take);
    }

    public async Task<InstitutionalRepositoryEntryListPage> ListPublicAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take <= 0 ? 50 : take, 1, 200);

        var items = await entries.ListPublicAsync(skip, take, cancellationToken).ConfigureAwait(false);
        return new InstitutionalRepositoryEntryListPage(items.Select(ToDto).ToList(), skip, take);
    }

    private async Task<Result> ValidateFacultyMemberAsync(Guid facultyMemberId, CancellationToken cancellationToken)
    {
        var facultyMember = await facultyMemberLookup.GetAsync(facultyMemberId, cancellationToken).ConfigureAwait(false);
        return facultyMember is null
            ? Result.Failure(Error.NotFound("facultymember.not_found", $"No FacultyMember exists with id '{facultyMemberId}'."))
            : Result.Success();
    }
}
