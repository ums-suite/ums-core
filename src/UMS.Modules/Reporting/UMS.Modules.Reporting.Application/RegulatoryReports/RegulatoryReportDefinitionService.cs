using System.Text.Json;
using UMS.Modules.Reporting.Application.Abstractions;
using UMS.Modules.Reporting.Application.Common;
using UMS.Modules.Reporting.Domain.RegulatoryReports;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Reporting.Application.RegulatoryReports;

/// <summary>RPT-11: <c>GET/POST /regulatory-reports/definitions</c>, <c>GET/PUT /definitions/{id}</c>. RPT-16: every create/edit is synchronously audited (ADR-0012).</summary>
public sealed class RegulatoryReportDefinitionService(
    IRegulatoryReportDefinitionRepository repository,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    public async Task<Result<RegulatoryReportDefinitionSummary>> CreateAsync(CreateRegulatoryReportDefinitionRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        if (await repository.ExistsByNameAsync(request.Name, cancellationToken).ConfigureAwait(false))
        {
            return Error.Conflict("regulatory_report_definition.duplicate_name", $"A RegulatoryReportDefinition named '{request.Name}' already exists.");
        }

        var created = RegulatoryReportDefinition.Create(
            request.Name,
            request.Category,
            request.Fields.Select(f => (f.FieldKey, f.Label)).ToList(),
            request.FiltersJson ?? "{}",
            request.SourceQueryReferencesJson ?? "[]",
            request.SupportedFormats,
            audit.ActorUserId,
            clock.UtcNow);

        if (created.IsFailure)
        {
            return created.Error!;
        }

        var definition = created.Value;
        repository.Add(definition);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest(
            "RegulatoryReportDefinition",
            definition.Id.Value.ToString(),
            "Created",
            beforeValueJson: null,
            afterValueJson: JsonSerializer.Serialize(ToSummary(definition)));

        var commit = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commit.IsFailure ? commit.Error! : ToSummary(definition);
    }

    public async Task<Result<RegulatoryReportDefinitionSummary>> UpdateAsync(Guid id, UpdateRegulatoryReportDefinitionRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var definition = await repository.GetByIdAsync(new RegulatoryReportDefinitionId(id), cancellationToken).ConfigureAwait(false);
        if (definition is null)
        {
            return Error.NotFound("regulatory_report_definition.not_found", $"RegulatoryReportDefinition '{id}' was not found.");
        }

        var beforeJson = JsonSerializer.Serialize(ToSummary(definition));

        var updated = definition.UpdateConfiguration(
            request.Name,
            request.Fields.Select(f => (f.FieldKey, f.Label)).ToList(),
            request.FiltersJson ?? "{}",
            request.SourceQueryReferencesJson ?? "[]",
            request.SupportedFormats,
            clock.UtcNow);

        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest(
            "RegulatoryReportDefinition",
            definition.Id.Value.ToString(),
            "Updated",
            beforeValueJson: beforeJson,
            afterValueJson: JsonSerializer.Serialize(ToSummary(definition)));

        var commit = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commit.IsFailure ? commit.Error! : ToSummary(definition);
    }

    public async Task<Result<RegulatoryReportDefinitionSummary>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var definition = await repository.GetByIdAsync(new RegulatoryReportDefinitionId(id), cancellationToken).ConfigureAwait(false);
        return definition is null
            ? Error.NotFound("regulatory_report_definition.not_found", $"RegulatoryReportDefinition '{id}' was not found.")
            : ToSummary(definition);
    }

    public async Task<RegulatoryReportDefinitionPageDto> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (normalizedPage - 1) * normalizedPageSize;

        var items = await repository.GetPageAsync(skip, normalizedPageSize, cancellationToken).ConfigureAwait(false);
        var totalCount = await repository.CountAsync(cancellationToken).ConfigureAwait(false);

        return new RegulatoryReportDefinitionPageDto(items.Select(ToSummary).ToList(), totalCount, normalizedPage, normalizedPageSize);
    }

    internal static RegulatoryReportDefinitionSummary ToSummary(RegulatoryReportDefinition definition) => new(
        definition.Id.Value,
        definition.Name,
        definition.Category,
        definition.FieldSelections.OrderBy(f => f.Ordinal).Select(f => new FieldSelectionDto(f.FieldKey, f.Label, f.Ordinal)).ToList(),
        definition.FiltersJson,
        definition.SourceQueryReferencesJson,
        definition.SupportedFormats,
        definition.IsActive,
        definition.CreatedAt,
        definition.UpdatedAt);
}

public sealed record FieldSelectionDto(string FieldKey, string Label, int Ordinal);

public sealed record CreateRegulatoryReportDefinitionRequest(
    string Name,
    RegulatoryReportCategory Category,
    IReadOnlyList<FieldSelectionRequest> Fields,
    string? FiltersJson,
    string? SourceQueryReferencesJson,
    RegulatoryReportFormat SupportedFormats);

public sealed record UpdateRegulatoryReportDefinitionRequest(
    string Name,
    IReadOnlyList<FieldSelectionRequest> Fields,
    string? FiltersJson,
    string? SourceQueryReferencesJson,
    RegulatoryReportFormat SupportedFormats);

public sealed record FieldSelectionRequest(string FieldKey, string Label);

public sealed record RegulatoryReportDefinitionSummary(
    Guid Id,
    string Name,
    RegulatoryReportCategory Category,
    IReadOnlyList<FieldSelectionDto> Fields,
    string FiltersJson,
    string SourceQueryReferencesJson,
    RegulatoryReportFormat SupportedFormats,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record RegulatoryReportDefinitionPageDto(IReadOnlyList<RegulatoryReportDefinitionSummary> Items, int TotalCount, int Page, int PageSize);
