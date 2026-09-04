using System.Text.Json;
using UMS.Modules.Organization.Application.Abstractions;
using UMS.Modules.Organization.Application.Common;
using UMS.Modules.Organization.Domain.Designations;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Organization.Application.Designations;

/// <summary>ORG-6: Designation CRUD (requirement-spec.md organization §2/§3/§6) - create-and-list only, per tickets.md's Flagged Gaps ("No PATCH/deactivate endpoint for Designation").</summary>
public sealed class DesignationService(
    IDesignationRepository designations,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    public static DesignationDto ToDto(Designation designation, string? languageCode) =>
        new(designation.Id.Value, designation.Title, designation.ResolveTitle(languageCode), designation.CreatedAt);

    public async Task<Result<DesignationDto>> CreateAsync(CreateDesignationRequest request, AuditContext audit, string? languageCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Error.Validation("designation.title_required", "Designation title is required.");
        }

        if (await designations.ExistsByTitleAsync(request.Title, cancellationToken).ConfigureAwait(false))
        {
            return Error.Conflict("designation.duplicate_title", $"A Designation titled '{request.Title}' already exists.");
        }

        var now = clock.UtcNow;
        var designation = Designation.Create(request.Title, now);
        if (request.Translations is not null)
        {
            foreach (var (languageCode2, title) in request.Translations)
            {
                designation.SetTranslation(languageCode2, title);
            }
        }

        designations.Add(designation);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var auditRequest = audit.ToRequest("Designation", designation.Id.Value.ToString(), AuditActions.Create, null, JsonSerializer.Serialize(new { title = designation.Title }));
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : ToDto(designation, languageCode);
    }

    public async Task<Result<DesignationDto>> GetByIdAsync(Guid id, string? languageCode, CancellationToken cancellationToken = default)
    {
        var designation = await designations.GetByIdAsync(new DesignationId(id), cancellationToken).ConfigureAwait(false);
        return designation is null
            ? Error.NotFound("designation.not_found", $"No Designation exists with id '{id}'.")
            : ToDto(designation, languageCode);
    }

    public async Task<DesignationListPage> ListAsync(int skip, int take, string? languageCode, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take, 1, 200);

        var items = await designations.ListAsync(skip, take, cancellationToken).ConfigureAwait(false);
        var total = await designations.CountAsync(cancellationToken).ConfigureAwait(false);
        return new DesignationListPage(items.Select(d => ToDto(d, languageCode)).ToList(), total, skip, take);
    }
}
