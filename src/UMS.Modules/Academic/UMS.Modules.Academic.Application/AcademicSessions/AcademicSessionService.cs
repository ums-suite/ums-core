using System.Text.Json;
using UMS.Modules.Academic.Application.Abstractions;
using UMS.Modules.Academic.Application.Common;
using UMS.Modules.Academic.Domain.AcademicSessions;
using UMS.Shared.Audit;
using UMS.Shared.Domain;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Academic.Application.AcademicSessions;

/// <summary>ACD-3 (bundled foundation): AcademicSession/Semester create - the registration/drop windows ACD-6/ACD-7 gate against (requirement-spec.md §2).</summary>
public sealed class AcademicSessionService(IAcademicSessionRepository sessions, IUnitOfWork unitOfWork, IAuditRecorder auditRecorder, IClock clock)
{
    public async Task<Result<AcademicSessionDto>> CreateAsync(CreateAcademicSessionRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var codeResult = AcademicSessionCode.Create(request.Code);
        if (codeResult.IsFailure)
        {
            return codeResult.Error!;
        }

        var session = AcademicSession.Create(codeResult.Value, clock.UtcNow);

        foreach (var semesterRequest in request.Semesters)
        {
            var registrationWindowResult = DateRange.Create(semesterRequest.RegistrationStart, semesterRequest.RegistrationEnd);
            if (registrationWindowResult.IsFailure)
            {
                return Error.Validation("academic_session.invalid_registration_window", registrationWindowResult.Error!.Message);
            }

            var dropWindowResult = DateRange.Create(semesterRequest.DropStart, semesterRequest.DropEnd);
            if (dropWindowResult.IsFailure)
            {
                return Error.Validation("academic_session.invalid_drop_window", dropWindowResult.Error!.Message);
            }

            try
            {
                session.AddSemester(semesterRequest.Name, registrationWindowResult.Value, dropWindowResult.Value);
            }
            catch (ArgumentException ex)
            {
                return Error.Validation("academic_session.invalid_semester", ex.Message);
            }
        }

        sessions.Add(session);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest("AcademicSession", session.Id.Value.ToString(), "create", null, JsonSerializer.Serialize(ToDto(session)));
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : ToDto(session);
    }

    public async Task<Result<AcademicSessionDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var session = await sessions.GetByIdAsync(new AcademicSessionId(id), cancellationToken).ConfigureAwait(false);
        return session is null ? Error.NotFound("academic_session.not_found", $"No AcademicSession exists with id '{id}'.") : ToDto(session);
    }

    internal static AcademicSessionDto ToDto(AcademicSession session) =>
        new(
            session.Id.Value,
            session.Code.Value,
            session.Semesters.Select(s => new SemesterDto(s.Id.Value, s.Name, s.RegistrationWindow.Start, s.RegistrationWindow.End, s.DropWindow.Start, s.DropWindow.End)).ToList(),
            session.CreatedAt);
}
