using System.Text.Json;
using UMS.Modules.Documents.Application.Abstractions;
using UMS.Modules.Documents.Domain.Common;
using UMS.Modules.Documents.Domain.GeneratedDocuments;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Documents.Application.Generation;

/// <summary>DOC-12: Admin/Registrar-only revoke, publishing <c>GeneratedDocumentRevoked</c> (§6) and, for an official-record type, a synchronous Audit entry (DOC-14, ADR-0012) - mirrors <c>UserStatusService</c>'s own same-transaction audit-write pattern.</summary>
public sealed class RevokeDocumentService(
    IGeneratedDocumentRepository documents,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    public async Task<Result<GeneratedDocumentDto>> RevokeAsync(Guid id, string reason, Guid actorUserId, string? actorIpAddress, string correlationId, CancellationToken cancellationToken = default)
    {
        var document = await documents.GetByIdAsync(new GeneratedDocumentId(id), cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return Error.NotFound("document.not_found", $"No GeneratedDocument exists with id '{id}'.");
        }

        // edge-cases.md's revoke decision: requires Ready - rejected against Pending/Uploaded/
        // already-terminal outright, never silently half-applied.
        var revoke = document.Revoke(reason, clock.UtcNow);
        if (revoke.IsFailure)
        {
            return revoke.Error!;
        }

        if (!document.DocumentType.IsOfficialRecord())
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return GeneratedDocumentDto.FromDomain(document, downloadUrl: null);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var auditRequest = new RecordAuditEntryRequest(
            ActorId: actorUserId.ToString(),
            ActorType: AuditActorType.User,
            IpAddress: actorIpAddress,
            Application: "ums-admin-web",
            EntityType: "GeneratedDocument",
            EntityId: document.Id.Value.ToString(),
            Action: AuditActions.Revoke,
            BeforeValueJson: JsonSerializer.Serialize(new { status = "Ready" }),
            AfterValueJson: JsonSerializer.Serialize(new { status = "Revoked", reason }),
            CorrelationId: correlationId,
            Reason: reason);

        var auditResult = await auditRecorder.RecordEntryAsync(auditRequest, transaction.DbTransaction, cancellationToken).ConfigureAwait(false);
        if (auditResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return auditResult.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return GeneratedDocumentDto.FromDomain(document, downloadUrl: null);
    }
}
