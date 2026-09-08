using System.Text.Json;
using UMS.Modules.Research.Application.Abstractions;
using UMS.Modules.Research.Application.Common;
using UMS.Modules.Research.Domain.Grants;
using UMS.Modules.Research.Domain.Publications;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Faculty;

namespace UMS.Modules.Research.Application.Publications;

/// <summary>
/// RES-6/RES-7/RES-8/RES-9: the Publication application service (requirement-spec.md §2 Publication
/// Records, §6 API Surface Publication rows). Authorship ownership ("any listed internal author or
/// Admin") is resolved by the calling Api layer, mirroring <c>GrantService</c>'s own posture.
/// </summary>
public sealed class PublicationService(
    IPublicationRepository publications,
    IGrantRepository grants,
    IFacultyMemberLookup facultyMemberLookup,
    PublicationDuplicateDetectionService duplicateDetection,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    public static PublicationDto ToDto(Publication publication) => new(
        publication.Id.Value,
        publication.Title,
        publication.Authors.OrderBy(a => a.Order).Select(a => new AuthorEntryDto(a.Order, a.FacultyMemberId, a.Name, a.Affiliation, a.IsCorrespondingAuthor)).ToList(),
        new VenueDto(publication.Venue.Type.ToString(), publication.Venue.Name, publication.Venue.Publisher),
        new CitationMetadataDto(publication.Citation.Doi, publication.Citation.PublicationDate, publication.Citation.CitationCount),
        publication.FundedByGrantIds.ToList(),
        publication.IsPubliclyVisible,
        publication.MergedIntoPublicationId,
        publication.CreatedAt,
        publication.UpdatedAt,
        publication.Version);

    public async Task<Result<PublicationDto>> CreateAsync(CreatePublicationRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var authorsResult = await BuildAndValidateAuthorsAsync(request.Authors, cancellationToken).ConfigureAwait(false);
        if (authorsResult.IsFailure)
        {
            return authorsResult.Error!;
        }

        if (!Enum.TryParse<VenueType>(request.Venue.Type, ignoreCase: true, out var venueType))
        {
            return Error.Validation("publication.invalid_venue_type", $"'{request.Venue.Type}' is not a recognized VenueType.");
        }

        var venueResult = Venue.Create(venueType, request.Venue.Name, request.Venue.Publisher);
        if (venueResult.IsFailure)
        {
            return venueResult.Error!;
        }

        var citationResult = CitationMetadata.Create(request.Citation.Doi, request.Citation.PublicationDate, request.Citation.CitationCount);
        if (citationResult.IsFailure)
        {
            return citationResult.Error!;
        }

        Publication publication;
        try
        {
            publication = Publication.Create(request.Title, authorsResult.Value, venueResult.Value, citationResult.Value, clock.UtcNow);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("publication.invalid", ex.Message);
        }

        publication.RaiseRecorded(clock.UtcNow);
        publications.Add(publication);

        // RES-9: the DOI-absent fuzzy-match half of duplicate detection runs BEFORE commit, in the
        // same transaction, so a flagged PublicationDuplicateCandidate and the new Publication row
        // are atomically consistent. The DOI-bearing half is the database's own hard unique
        // constraint, translated to a Conflict result by TransactionalAuditWriter below.
        await duplicateDetection.DetectAsync(publication, cancellationToken).ConfigureAwait(false);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest("Publication", publication.Id.Value.ToString(), AuditActions.Create, null, JsonSerializer.Serialize(new { title = publication.Title, doi = publication.Citation.Doi }));
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : ToDto(publication);
    }

    public async Task<Result<PublicationDto>> UpdateAsync(Guid id, UpdatePublicationRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var publication = await publications.GetByIdAsync(new PublicationId(id), cancellationToken).ConfigureAwait(false);
        if (publication is null)
        {
            return Error.NotFound("publication.not_found", $"No Publication exists with id '{id}'.");
        }

        var authorsResult = await BuildAndValidateAuthorsAsync(request.Authors, cancellationToken).ConfigureAwait(false);
        if (authorsResult.IsFailure)
        {
            return authorsResult.Error!;
        }

        if (!Enum.TryParse<VenueType>(request.Venue.Type, ignoreCase: true, out var venueType))
        {
            return Error.Validation("publication.invalid_venue_type", $"'{request.Venue.Type}' is not a recognized VenueType.");
        }

        var venueResult = Venue.Create(venueType, request.Venue.Name, request.Venue.Publisher);
        if (venueResult.IsFailure)
        {
            return venueResult.Error!;
        }

        var citationResult = CitationMetadata.Create(request.Citation.Doi, request.Citation.PublicationDate, request.Citation.CitationCount);
        if (citationResult.IsFailure)
        {
            return citationResult.Error!;
        }

        var beforeJson = JsonSerializer.Serialize(new { title = publication.Title, doi = publication.Citation.Doi });

        try
        {
            publication.Update(request.Title, authorsResult.Value, venueResult.Value, citationResult.Value, clock.UtcNow);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("publication.invalid", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Conflict("publication.merged", ex.Message);
        }

        unitOfWork.SetExpectedVersion(publication, request.Version);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest("Publication", publication.Id.Value.ToString(), AuditActions.Update, beforeJson, JsonSerializer.Serialize(new { title = publication.Title, doi = publication.Citation.Doi }));
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : ToDto(publication);
    }

    /// <summary>RES-9: Admin-only resolution of a <see cref="PublicationDuplicateCandidate"/> (or a direct Admin call without one) - <paramref name="id"/> is the surviving Publication, absorbing <paramref name="request"/>'s <see cref="MergePublicationsRequest.MergedPublicationId"/>'s Grant-funding links. Never automatic (design-decisions.md).</summary>
    public async Task<Result<PublicationDto>> MergeAsync(Guid id, MergePublicationsRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        if (id == request.MergedPublicationId)
        {
            return Error.Validation("publication.merge_self", "A Publication cannot be merged into itself.");
        }

        var surviving = await publications.GetByIdAsync(new PublicationId(id), cancellationToken).ConfigureAwait(false);
        if (surviving is null)
        {
            return Error.NotFound("publication.not_found", $"No Publication exists with id '{id}'.");
        }

        var merged = await publications.GetByIdAsync(new PublicationId(request.MergedPublicationId), cancellationToken).ConfigureAwait(false);
        if (merged is null)
        {
            return Error.NotFound("publication.not_found", $"No Publication exists with id '{request.MergedPublicationId}'.");
        }

        try
        {
            surviving.AbsorbMerge(merged.FundedByGrantIds, merged.Id.Value, clock.UtcNow);
            merged.MarkMergedInto(surviving.Id.Value);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Conflict("publication.already_merged", ex.Message);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest(
            "Publication",
            surviving.Id.Value.ToString(),
            "merge",
            JsonSerializer.Serialize(new { survivingId = surviving.Id.Value, mergedId = merged.Id.Value }),
            JsonSerializer.Serialize(new { survivingFundedByGrantIds = surviving.FundedByGrantIds }));
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : ToDto(surviving);
    }

    /// <summary>RES-8: requirement-spec.md §4 fifth bullet - only a Grant that has reached Funded/Active/Closed/Reported may be cited; a Proposed or Rejected Grant can never be cited. Validated by a lightweight status-only lookup (never a full Grant object-graph load, §3/§9).</summary>
    public async Task<Result<PublicationDto>> AddFundedByGrantAsync(Guid id, Guid grantId, CancellationToken cancellationToken = default)
    {
        var publication = await publications.GetByIdAsync(new PublicationId(id), cancellationToken).ConfigureAwait(false);
        if (publication is null)
        {
            return Error.NotFound("publication.not_found", $"No Publication exists with id '{id}'.");
        }

        var status = await grants.GetStatusAsync(grantId, cancellationToken).ConfigureAwait(false);
        if (status is null)
        {
            return Error.NotFound("grant.not_found", $"No Grant exists with id '{grantId}'.");
        }

        if (status is not (GrantStatus.Funded or GrantStatus.Active or GrantStatus.Closed or GrantStatus.Reported))
        {
            return Error.Validation("publication.grant_not_eligible", $"Grant '{grantId}' is in status {status} - only Funded/Active/Closed/Reported Grants may be cited as a funding source.");
        }

        publication.AddFundedByGrant(grantId);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(publication);
    }

    public async Task<Result<PublicationDto>> RemoveFundedByGrantAsync(Guid id, Guid grantId, CancellationToken cancellationToken = default)
    {
        var publication = await publications.GetByIdAsync(new PublicationId(id), cancellationToken).ConfigureAwait(false);
        if (publication is null)
        {
            return Error.NotFound("publication.not_found", $"No Publication exists with id '{id}'.");
        }

        publication.RemoveFundedByGrant(grantId);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(publication);
    }

    public async Task<Result<PublicationDto>> SetPubliclyVisibleAsync(Guid id, bool isPubliclyVisible, CancellationToken cancellationToken = default)
    {
        var publication = await publications.GetByIdAsync(new PublicationId(id), cancellationToken).ConfigureAwait(false);
        if (publication is null)
        {
            return Error.NotFound("publication.not_found", $"No Publication exists with id '{id}'.");
        }

        publication.SetPubliclyVisible(isPubliclyVisible);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(publication);
    }

    public async Task<Result<PublicationDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var publication = await publications.GetByIdAsync(new PublicationId(id), cancellationToken).ConfigureAwait(false);
        return publication is null ? Error.NotFound("publication.not_found", $"No Publication exists with id '{id}'.") : ToDto(publication);
    }

    public async Task<PublicationListPage> ListAsync(Guid? authorFacultyMemberId, Guid? grantId, int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take <= 0 ? 50 : take, 1, 200);

        var items = await publications.ListAsync(authorFacultyMemberId, grantId, skip, take, cancellationToken).ConfigureAwait(false);
        return new PublicationListPage(items.Select(ToDto).ToList(), skip, take);
    }

    public async Task<PublicationListPage> ListPubliclyVisibleAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take <= 0 ? 50 : take, 1, 200);

        var items = await publications.ListPubliclyVisibleAsync(skip, take, cancellationToken).ConfigureAwait(false);
        return new PublicationListPage(items.Select(ToDto).ToList(), skip, take);
    }

    private async Task<Result<List<AuthorEntry>>> BuildAndValidateAuthorsAsync(IReadOnlyList<AuthorEntryDto> authors, CancellationToken cancellationToken)
    {
        var entries = new List<AuthorEntry>();
        foreach (var author in authors)
        {
            if (author.FacultyMemberId is { } facultyMemberId)
            {
                // requirement-spec.md item 13: every internal AuthorEntry's FacultyMemberId is
                // validated against IFacultyMemberLookup at write time.
                var facultyMember = await facultyMemberLookup.GetAsync(facultyMemberId, cancellationToken).ConfigureAwait(false);
                if (facultyMember is null)
                {
                    return Result.Failure<List<AuthorEntry>>(Error.NotFound("facultymember.not_found", $"No FacultyMember exists with id '{facultyMemberId}'."));
                }
            }

            entries.Add(new AuthorEntry(author.Order, author.FacultyMemberId, author.Name, author.Affiliation, author.IsCorrespondingAuthor));
        }

        return entries;
    }
}
