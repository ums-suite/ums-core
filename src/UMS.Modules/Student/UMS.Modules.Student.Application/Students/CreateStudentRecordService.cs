using System.Text.Json;
using Microsoft.Extensions.Logging;
using UMS.Modules.Student.Application.Abstractions;
using UMS.Modules.Student.Application.Common;
using UMS.Modules.Student.Domain.Students;
using UMS.Shared.Audit;
using UMS.Shared.Domain;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Student.Application.Students;

/// <summary>
/// STU-1..STU-4: the sole application-service entry point for creating a <c>Student</c>
/// (requirement-spec.md student §2 Applicant → Student Creation Handoff, §6: internal-only,
/// exposed to other modules only via <c>UMS.Shared.Student.IStudentRecordProvisioner</c>).
///
/// <para>
/// <b>Design decision, documented here since neither design-decisions.md nor requirement-spec.md
/// fully resolves the ordering (this module's own PR description restates this):</b> the
/// <c>Student</c> row itself is the ONE authoritative, transactionally-safe write - it is never
/// rolled back by a downstream integration failure. Identity provisioning (STU-2), the Documents
/// ID-card request (STU-3), and the Notifications welcome message (STU-4) all run AFTER that write
/// commits, each independently best-effort (caught, logged, never re-thrown to the caller) - the
/// same posture Documents' own <c>GenerateDocumentService.PublishNotificationSafelyAsync</c>
/// already established for its own downstream Notifications call, extended here to two more
/// downstream integrations. Building genuine cross-module saga/compensation machinery so that an
/// Identity outage could roll back an already-legitimate Student record would reintroduce exactly
/// the complexity ADR-0001's own "real database transactions instead of sagas" trade-off exists to
/// avoid - and would leave the Applicant with NO Student record at all on a downstream hiccup,
/// which is a worse outcome than a Student record with a follow-up-needed <c>IdentityUserId</c> of
/// <c>null</c> (visible on every returned <see cref="StudentDto"/> for exactly this reason).
/// </para>
/// </summary>
public sealed class CreateStudentRecordService(
    IStudentRepository students,
    IOrganizationDepartmentExistenceChecker departmentChecker,
    IProgramExistenceChecker programChecker,
    IStudentNumberSequence studentNumberSequence,
    IUserProvisioningPort userProvisioning,
    IDocumentGenerationPort documentGeneration,
    INotificationRequestPublisher notifications,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock,
    ILogger<CreateStudentRecordService> logger)
{
    private const int MaxStudentNumberCollisionRetries = 3;

    public static StudentDto ToDto(Domain.Students.Student student) => new(
        student.Id.Value,
        student.StudentNumber.Value,
        student.DepartmentId,
        student.ProgramId,
        student.Name.GivenName,
        student.Name.FamilyName,
        student.Name.GivenNameBn,
        student.Name.FamilyNameBn,
        student.Email.Value,
        student.Mobile?.Value,
        student.DateOfBirth,
        student.NationalId,
        student.Status.ToString(),
        student.IdentityUserId,
        student.IdCardDocumentId,
        student.ContactEmail,
        student.ContactPhone,
        student.PhotoUrl,
        student.CreatedAt,
        student.Version);

    public async Task<Result<StudentDto>> CreateAsync(CreateStudentRecordRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        // edge-cases.md "Admission's confirmation call to CreateStudentRecord is retried": the
        // fast-path pre-check. The actual guard is the DB unique constraint on
        // originating_application_id, caught below - this is a convenience short-circuit only.
        var existing = await students.GetByOriginatingApplicationIdAsync(request.OriginatingApplicationId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return ToDto(existing);
        }

        var nameResult = PersonName.Create(request.GivenName, request.FamilyName, request.GivenNameBn, request.FamilyNameBn);
        if (nameResult.IsFailure)
        {
            return nameResult.Error!;
        }

        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure)
        {
            return emailResult.Error!;
        }

        PhoneNumber? mobile = null;
        if (!string.IsNullOrWhiteSpace(request.Mobile))
        {
            var mobileResult = PhoneNumber.Create(request.Mobile);
            if (mobileResult.IsFailure)
            {
                return mobileResult.Error!;
            }

            mobile = mobileResult.Value;
        }

        var facultyCode = request.FacultyCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!StudentNumber.IsValidFacultyCode(facultyCode))
        {
            return Error.Validation("student.invalid_faculty_code", "Faculty code must be 1-10 upper-case letters/digits.");
        }

        if (request.AdmissionYear is < 2000 or > 2100)
        {
            return Error.Validation("student.invalid_admission_year", "Admission year is out of range.");
        }

        if (!await departmentChecker.ExistsAsync(request.DepartmentId, cancellationToken).ConfigureAwait(false))
        {
            return Error.NotFound("department.not_found", $"No Department exists with id '{request.DepartmentId}'.");
        }

        if (!await programChecker.ExistsAsync(request.ProgramId, cancellationToken).ConfigureAwait(false))
        {
            return Error.NotFound("program.not_found", $"No Program exists with id '{request.ProgramId}'.");
        }

        for (var attempt = 1; attempt <= MaxStudentNumberCollisionRetries; attempt++)
        {
            var sequence = await studentNumberSequence.NextAsync(request.AdmissionYear, facultyCode, cancellationToken).ConfigureAwait(false);
            var studentNumber = StudentNumber.FromIssuedSequence(request.AdmissionYear, facultyCode, sequence);

            var now = clock.UtcNow;
            Domain.Students.Student student;
            try
            {
                student = Domain.Students.Student.Enroll(request.OriginatingApplicationId, studentNumber, request.DepartmentId, request.ProgramId, nameResult.Value, emailResult.Value, mobile, request.DateOfBirth, request.NationalId, now);
            }
            catch (ArgumentException ex)
            {
                return Error.Validation("student.invalid", ex.Message);
            }

            students.Add(student);

            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var auditRequest = audit.ToRequest(
                "Student",
                student.Id.Value.ToString(),
                AuditActions.Create,
                null,
                JsonSerializer.Serialize(new { studentNumber = studentNumber.Value, departmentId = request.DepartmentId, programId = request.ProgramId }),
                organizationScopeId: request.DepartmentId);

            var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
            if (commitResult.IsSuccess)
            {
                // design-decisions.md's residual note: side effects fire ONLY on this first,
                // successful insert branch - never on the idempotent lookup-and-return branch above.
                await RunBestEffortSideEffectsAsync(student, audit.CorrelationId, cancellationToken).ConfigureAwait(false);
                return ToDto(student);
            }

            // edge-cases.md "StudentNumber generation collides under concurrent creation load":
            // the sequence eliminates the numeric-component race in the common case, but the
            // defense-in-depth full-string unique constraint is retried here a bounded number of
            // times before giving up, exactly as that edge case's resolution requires.
            if (commitResult.Error!.Code == "student.duplicate_value")
            {
                // A concurrent caller may have won the ORIGINATING-APPLICATION race instead of the
                // student-number race (edge-cases.md's other retried-call scenario, hitting this
                // same code path from a genuinely simultaneous duplicate call) - resolve to the
                // winner's row exactly like Documents' own idempotent-generation-race handling.
                var winner = await students.GetByOriginatingApplicationIdAsync(request.OriginatingApplicationId, cancellationToken).ConfigureAwait(false);
                if (winner is not null)
                {
                    return ToDto(winner);
                }

                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning("StudentNumber collision on attempt {Attempt}/{MaxAttempts} for ({AdmissionYear}, {FacultyCode}) - retrying [correlationId={CorrelationId}].", attempt, MaxStudentNumberCollisionRetries, request.AdmissionYear, facultyCode, audit.CorrelationId);
                }

                continue;
            }

            return commitResult.Error!;
        }

        return Error.Conflict("student.student_number_generation_failed", "StudentNumber generation repeatedly collided - retry the request.");
    }

    /// <summary>See this class's own remarks for why every one of these is caught and logged, never re-thrown.</summary>
    private async Task RunBestEffortSideEffectsAsync(Domain.Students.Student student, string correlationId, CancellationToken cancellationToken)
    {
        Guid? identityUserId = null;

        try
        {
            var provisionRequest = new ProvisionStudentUserRequest(
                Username: student.StudentNumber.Value.ToLowerInvariant(),
                Email: student.Email.Value,
                GivenName: student.Name.GivenName,
                FamilyName: student.Name.FamilyName,
                GivenNameBn: student.Name.GivenNameBn,
                FamilyNameBn: student.Name.FamilyNameBn,
                Mobile: student.Mobile?.Value);

            var outcome = await userProvisioning.ProvisionAsync(provisionRequest, cancellationToken).ConfigureAwait(false);
            if (outcome.Succeeded && outcome.UserId is { } userId)
            {
                identityUserId = userId;
                student.SetIdentityUser(userId);
            }
            else if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning("Identity provisioning did not succeed for Student {StudentId}: {Reason} [correlationId={CorrelationId}].", student.Id.Value, outcome.FailureReason, correlationId);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Identity provisioning threw for Student {StudentId} - the Student record itself is unaffected [correlationId={CorrelationId}].", student.Id.Value, correlationId);
        }

        try
        {
            var idCardRequest = new RequestStudentIdCardRequest(student.Id.Value, student.StudentNumber.Value, student.Name.DisplayName, identityUserId, correlationId);
            var outcome = await documentGeneration.RequestIdCardAsync(idCardRequest, cancellationToken).ConfigureAwait(false);
            if (outcome.Succeeded && outcome.DocumentId is { } documentId)
            {
                student.SetIdCardDocument(documentId);
            }
            else if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning("ID-card generation did not succeed for Student {StudentId}: {Reason} [correlationId={CorrelationId}].", student.Id.Value, outcome.FailureReason, correlationId);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "ID-card generation threw for Student {StudentId} - the Student record itself is unaffected [correlationId={CorrelationId}].", student.Id.Value, correlationId);
        }

        // Persist whatever best-effort bookkeeping succeeded above - a plain follow-up save, not
        // re-audited (these are non-sensitive references to already-created side-effect records,
        // not a new sensitive mutation of the Student itself).
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to persist Identity/Documents cross-references for Student {StudentId} [correlationId={CorrelationId}].", student.Id.Value, correlationId);
        }

        // ADR-0009: Notifications is always best-effort/non-blocking, never a call the primary
        // flow depends on - mirrors Documents' own PublishNotificationSafelyAsync exactly.
        if (identityUserId is { } recipientId)
        {
            try
            {
                await notifications.PublishAsync(
                    new NotificationRequest(
                        recipientId,
                        "StudentRecordCreated",
                        student.Id.Value.ToString(),
                        new Dictionary<string, string> { ["studentNumber"] = student.StudentNumber.Value, ["displayName"] = student.Name.DisplayName },
                        DedupeKey: student.Id.Value.ToString()),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Welcome NotificationRequest publish failed for Student {StudentId} - the Student record itself is unaffected [correlationId={CorrelationId}].", student.Id.Value, correlationId);
            }
        }
        else if (logger.IsEnabled(LogLevel.Information))
        {
            // No Identity User to notify - Notifications addresses by Identity UserId (glossary),
            // so there is genuinely no recipient to send a welcome message to until provisioning
            // is retried/repaired.
            logger.LogInformation("Skipping welcome NotificationRequest for Student {StudentId} - no IdentityUserId was provisioned [correlationId={CorrelationId}].", student.Id.Value, correlationId);
        }
    }
}
