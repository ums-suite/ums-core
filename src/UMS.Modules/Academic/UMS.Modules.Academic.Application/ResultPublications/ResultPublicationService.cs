using System.Text.Json;
using Microsoft.Extensions.Logging;
using UMS.Modules.Academic.Application.Abstractions;
using UMS.Modules.Academic.Application.Common;
using UMS.Modules.Academic.Domain.Events;
using UMS.Modules.Academic.Domain.ResultPublications;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Academic.Application.ResultPublications;

/// <summary>
/// ACD-11/ACD-12: the Department-Head lock/reject review step, authority approval, publish, and
/// archive - every transition a state-guarded conditional update (design-decisions.md "Grade-Lock
/// State Machine Design" / "Concurrent Grade-Batch Review Conflict Resolution"), the SAME code path
/// resolving both the Faculty-submission-races-lock edge case (see <c>GradeService</c>) and the
/// concurrent-reviewer-race edge case (two reviewers acting on the same batch - whichever commits
/// first wins; the loser gets an explicit, named rejection identifying the current authoritative
/// state, never a silent overwrite).
///
/// <para>
/// requirement-spec.md §5's 100%-trace-coverage NFR: <see cref="LockAsync"/> and
/// <see cref="PublishAsync"/> (the two events named directly in ADR-0003's own example list,
/// <c>GradeLocked</c>/<c>ResultPublished</c>) log a structured line carrying `correlationId` +
/// `resultPublicationId` on every call, success or rejection.
/// </para>
/// </summary>
public sealed class ResultPublicationService(
    IResultPublicationRepository resultPublications,
    IUnitOfWork unitOfWork,
    IDomainEventRecorder eventRecorder,
    IAuditRecorder auditRecorder,
    IClock clock,
    ILogger<ResultPublicationService> logger)
{
    public async Task<Result<ResultPublicationDto>> LockAsync(Guid courseOfferingId, Guid lockedByUserId, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var current = await resultPublications.GetByCourseOfferingIdAsync(courseOfferingId, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            return Error.NotFound("resultpublication.not_found", $"No ResultPublication exists for CourseOffering '{courseOfferingId}' - no grades have been submitted yet.");
        }

        var now = clock.UtcNow;
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var transitioned = await resultPublications.TryTransitionAsync(
            current.Id,
            [ResultPublicationStatus.Calculated],
            ResultPublicationStatus.Verified,
            columns =>
            {
                columns.LockedAt = now;
                columns.LockedByUserId = lockedByUserId;
            },
            cancellationToken).ConfigureAwait(false);

        if (!transitioned)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            var actual = await resultPublications.GetByCourseOfferingIdAsync(courseOfferingId, cancellationToken).ConfigureAwait(false);
            var actualStatus = actual?.Status;
            var rejectedResultPublicationId = current.Id.Value;
            var rejectedCorrelationId = audit.CorrelationId;
            logger.LogWarning(
                "GradeLocked rejected: ResultPublication {ResultPublicationId} for CourseOffering {CourseOfferingId} is already '{ActualStatus}' (correlationId {CorrelationId}).",
                rejectedResultPublicationId,
                courseOfferingId,
                actualStatus,
                rejectedCorrelationId);
            return Error.Conflict("resultpublication.already_reviewed", $"CourseOffering '{courseOfferingId}''s grade batch is already '{actualStatus}' - it cannot be locked again from that state.");
        }

        eventRecorder.Enqueue(new GradeLocked(current.Id.Value, courseOfferingId, lockedByUserId, now));

        var auditRequest = audit.ToRequest("ResultPublication", current.Id.Value.ToString(), "lock", JsonSerializer.Serialize(new { status = "Calculated" }), JsonSerializer.Serialize(new { status = "Verified" }));
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (commitResult.IsFailure)
        {
            return commitResult.Error!;
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "GradeLocked: ResultPublication {ResultPublicationId} for CourseOffering {CourseOfferingId} locked by {LockedByUserId} (correlationId {CorrelationId}).",
                current.Id.Value,
                courseOfferingId,
                lockedByUserId,
                audit.CorrelationId);
        }

        var refreshed = await resultPublications.GetByCourseOfferingIdAsync(courseOfferingId, cancellationToken).ConfigureAwait(false);
        return ToDto(refreshed!);
    }

    /// <summary>ACD-11 (reject path): returns the batch to Faculty for re-entry - status stays `Calculated` (requirement-spec.md §8).</summary>
    public async Task<Result<ResultPublicationDto>> RejectAsync(Guid courseOfferingId, Guid rejectedByUserId, RejectGradeBatchRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var current = await resultPublications.GetByCourseOfferingIdAsync(courseOfferingId, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            return Error.NotFound("resultpublication.not_found", $"No ResultPublication exists for CourseOffering '{courseOfferingId}'.");
        }

        var now = clock.UtcNow;
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var rejected = await resultPublications.TryRejectAsync(current.Id, request.Reason, rejectedByUserId, now, cancellationToken).ConfigureAwait(false);
        if (!rejected)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            var actual = await resultPublications.GetByCourseOfferingIdAsync(courseOfferingId, cancellationToken).ConfigureAwait(false);
            return Error.Conflict("resultpublication.already_reviewed", $"CourseOffering '{courseOfferingId}''s grade batch is already '{actual?.Status}' - it cannot be rejected from that state.");
        }

        var auditRequest = audit.ToRequest("ResultPublication", current.Id.Value.ToString(), "reject", null, JsonSerializer.Serialize(new { }), reason: request.Reason);
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (commitResult.IsFailure)
        {
            return commitResult.Error!;
        }

        var refreshed = await resultPublications.GetByCourseOfferingIdAsync(courseOfferingId, cancellationToken).ConfigureAwait(false);
        return ToDto(refreshed!);
    }

    public async Task<Result<ResultPublicationDto>> ApproveAsync(Guid courseOfferingId, Guid approvedByUserId, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var approvedAt = clock.UtcNow;
        return await TransitionAsync(
            courseOfferingId,
            ResultPublicationStatus.Verified,
            ResultPublicationStatus.Approved,
            "approve",
            columns =>
            {
                columns.ApprovedAt = approvedAt;
                columns.ApprovedByUserId = approvedByUserId;
            },
            audit,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<ResultPublicationDto>> PublishAsync(Guid courseOfferingId, Guid publishedByUserId, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var publishedAt = clock.UtcNow;
        var result = await TransitionAsync(
            courseOfferingId,
            ResultPublicationStatus.Approved,
            ResultPublicationStatus.Published,
            "publish",
            columns =>
            {
                columns.PublishedAt = publishedAt;
                columns.PublishedByUserId = publishedByUserId;
            },
            audit,
            cancellationToken).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            var resultPublicationId = result.Value.Id;
            eventRecorder.Enqueue(new ResultPublished(resultPublicationId, courseOfferingId, publishedByUserId, publishedAt));
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "ResultPublished: ResultPublication {ResultPublicationId} for CourseOffering {CourseOfferingId} published by {PublishedByUserId} (correlationId {CorrelationId}).",
                    resultPublicationId,
                    courseOfferingId,
                    publishedByUserId,
                    audit.CorrelationId);
            }

            // The ResultPublished event was enqueued after TransitionAsync's own transaction
            // already committed - flush it via one more SaveChanges so it reaches the outbox.
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var errorCode = result.Error!.Code;
            var correlationId = audit.CorrelationId;
            logger.LogWarning(
                "ResultPublished rejected for CourseOffering {CourseOfferingId}: {ErrorCode} (correlationId {CorrelationId}).",
                courseOfferingId,
                errorCode,
                correlationId);
        }

        return result;
    }

    public async Task<Result<ResultPublicationDto>> ArchiveAsync(Guid courseOfferingId, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var archivedAt = clock.UtcNow;
        return await TransitionAsync(
            courseOfferingId,
            ResultPublicationStatus.Published,
            ResultPublicationStatus.Archived,
            "archive",
            columns => columns.ArchivedAt = archivedAt,
            audit,
            cancellationToken).ConfigureAwait(false);
    }

    internal static ResultPublicationDto ToDto(Domain.ResultPublications.ResultPublication resultPublication) =>
        new(
            resultPublication.Id.Value,
            resultPublication.CourseOfferingId,
            resultPublication.Status.ToString(),
            resultPublication.CalculatedAt,
            resultPublication.RejectedAt,
            resultPublication.RejectionReason,
            resultPublication.LockedAt,
            resultPublication.ApprovedAt,
            resultPublication.PublishedAt,
            resultPublication.ArchivedAt,
            resultPublication.CorrectionCount);

    private async Task<Result<ResultPublicationDto>> TransitionAsync(
        Guid courseOfferingId,
        ResultPublicationStatus expectedPriorStatus,
        ResultPublicationStatus newStatus,
        string action,
        Action<ResultPublicationTransitionColumns> applyColumns,
        AuditContext audit,
        CancellationToken cancellationToken)
    {
        var current = await resultPublications.GetByCourseOfferingIdAsync(courseOfferingId, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            return Error.NotFound("resultpublication.not_found", $"No ResultPublication exists for CourseOffering '{courseOfferingId}'.");
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var transitioned = await resultPublications.TryTransitionAsync(current.Id, [expectedPriorStatus], newStatus, applyColumns, cancellationToken).ConfigureAwait(false);
        if (!transitioned)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            var actual = await resultPublications.GetByCourseOfferingIdAsync(courseOfferingId, cancellationToken).ConfigureAwait(false);
            return Error.Conflict("resultpublication.invalid_transition", $"CourseOffering '{courseOfferingId}''s grade batch is '{actual?.Status}', not '{expectedPriorStatus}' - cannot {action} it.");
        }

        var auditRequest = audit.ToRequest("ResultPublication", current.Id.Value.ToString(), action, JsonSerializer.Serialize(new { status = expectedPriorStatus.ToString() }), JsonSerializer.Serialize(new { status = newStatus.ToString() }));
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (commitResult.IsFailure)
        {
            return commitResult.Error!;
        }

        var refreshed = await resultPublications.GetByCourseOfferingIdAsync(courseOfferingId, cancellationToken).ConfigureAwait(false);
        return ToDto(refreshed!);
    }
}
