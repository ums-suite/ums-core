using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Application.Common;
using UMS.Modules.Alumni.Domain.Alumni;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Student;

namespace UMS.Modules.Alumni.Application.Alumni;

/// <summary>
/// ALM-1/ALM-2/ALM-3: Student→Alumnus transition, own-profile management, and directory search.
/// </summary>
public sealed class AlumnusService(
    IAlumnusRepository alumni,
    IStudentStatusChecker studentStatusChecker,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder)
{
    public static AlumnusDto ToDto(Alumnus alumnus) => new(
        alumnus.Id.Value,
        alumnus.StudentIdRef,
        alumnus.GraduationYear,
        alumnus.ProgramId,
        alumnus.DepartmentId,
        alumnus.ProfileVisibility.ToString(),
        alumnus.CurrentEmployer,
        alumnus.Bio,
        alumnus.Location,
        alumnus.ContactEmail,
        alumnus.ContactPhone,
        alumnus.HideCurrentEmployer,
        alumnus.HideContactDetails,
        alumnus.CreatedAt,
        alumnus.Version);

    /// <summary>
    /// ALM-1: the Student→Alumnus transition (requirement-spec.md §2.1). design-decisions.md
    /// "Idempotent StudentGraduated Consumption": the DB unique constraint on <c>student_id_ref</c>
    /// is the ONLY correctness mechanism - the pre-check below is purely a happy-path optimization to
    /// avoid an unnecessary round-trip on the (overwhelmingly common) non-duplicate case, never relied
    /// upon for correctness under concurrent delivery (edge-cases.md "Duplicate StudentGraduated
    /// delivered concurrently by two consumer instances").
    /// </summary>
    public async Task<Result<AlumnusDto>> CreateFromStudentGraduationAsync(Guid studentId, DateTimeOffset occurredAt, CancellationToken cancellationToken = default)
    {
        var existing = await alumni.GetByStudentIdRefAsync(studentId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return ToDto(existing);
        }

        var standing = await studentStatusChecker.GetByStudentIdAsync(studentId, cancellationToken).ConfigureAwait(false);
        if (standing is null)
        {
            // Genuinely transient (Student's own record not yet visible via the shared read contract) -
            // the relay worker leaves this event unmarked so the next poll retries it, rather than
            // silently dropping a graduation.
            return Error.NotFound("alumnus.student_not_found", $"No Student record is resolvable for StudentId '{studentId}' yet.");
        }

        var alumnus = Domain.Alumni.Alumnus.Create(studentId, occurredAt.Year, standing.ProgramId, standing.DepartmentId, occurredAt);
        alumni.Add(alumnus);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DuplicateValueException)
        {
            // The genuine idempotent no-op path (design-decisions.md): a concurrently-delivered copy
            // of the same event already won the race and committed first.
            var reloaded = await alumni.GetByStudentIdRefAsync(studentId, cancellationToken).ConfigureAwait(false);
            return reloaded is not null
                ? ToDto(reloaded)
                : Error.Failure("alumnus.create_race_unresolved", $"Unique-violation on student_id_ref '{studentId}' but no Alumnus is now readable - this should not happen.");
        }

        return ToDto(alumnus);
    }

    public async Task<Result<AlumnusDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var alumnus = await alumni.GetByIdAsync(new AlumnusId(id), cancellationToken).ConfigureAwait(false);
        return alumnus is null ? Error.NotFound("alumnus.not_found", $"No Alumnus exists with id '{id}'.") : ToDto(alumnus);
    }

    public async Task<Result<AlumnusDto>> GetByStudentIdRefAsync(Guid studentIdRef, CancellationToken cancellationToken = default)
    {
        var alumnus = await alumni.GetByStudentIdRefAsync(studentIdRef, cancellationToken).ConfigureAwait(false);
        return alumnus is null ? Error.NotFound("alumnus.not_found", $"No Alumnus exists for StudentId '{studentIdRef}'.") : ToDto(alumnus);
    }

    /// <summary>ALM-2: self-service profile update - no Permission gate, ownership is enforced by the calling Api layer resolving the caller's OWN AlumnusId.</summary>
    public async Task<Result<AlumnusDto>> UpdateProfileAsync(Guid id, UpdateAlumnusProfileRequest request, CancellationToken cancellationToken = default)
    {
        var alumnus = await alumni.GetByIdAsync(new AlumnusId(id), cancellationToken).ConfigureAwait(false);
        if (alumnus is null)
        {
            return Error.NotFound("alumnus.not_found", $"No Alumnus exists with id '{id}'.");
        }

        alumnus.UpdateProfile(request.CurrentEmployer, request.Bio, request.Location, request.ContactEmail, request.ContactPhone, request.HideCurrentEmployer, request.HideContactDetails);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(alumnus);
    }

    /// <summary>
    /// ALM-2 (self) / ALM-16 (Admin override, audited - requirement-spec.md §5). <paramref name="audit"/>
    /// is non-null ONLY for an Admin-initiated override of someone else's visibility - a self-service
    /// change is not itself a "sensitive mutation" the spec lists in §5, so it commits without an
    /// audit write (mirrors every other module's "audit only what the spec actually names" discipline).
    /// </summary>
    public async Task<Result<AlumnusDto>> SetVisibilityAsync(Guid id, ProfileVisibility visibility, AuditContext? audit, CancellationToken cancellationToken = default)
    {
        var alumnus = await alumni.GetByIdAsync(new AlumnusId(id), cancellationToken).ConfigureAwait(false);
        if (alumnus is null)
        {
            return Error.NotFound("alumnus.not_found", $"No Alumnus exists with id '{id}'.");
        }

        var before = alumnus.ProfileVisibility;
        alumnus.SetVisibility(visibility);

        if (audit is null)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return ToDto(alumnus);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest(
            "Alumnus",
            alumnus.Id.Value.ToString(),
            AuditActions.Update,
            $"{{\"profileVisibility\":\"{before}\"}}",
            $"{{\"profileVisibility\":\"{alumnus.ProfileVisibility}\"}}",
            reason: "admin_override");
        var committed = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return committed.IsFailure ? committed.Error! : ToDto(alumnus);
    }

    /// <summary>ALM-3: requirement-spec.md §2.2 directory search - Public-only unless <paramref name="bypassPrivacy"/> (Admin, audited by the CALLING endpoint per §5) or the record is the caller's own.</summary>
    public async Task<AlumniDirectoryPage> SearchDirectoryAsync(AlumniDirectoryFilter filter, Guid? callerAlumnusId, bool bypassPrivacy, int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take <= 0 ? 50 : take, 1, 200);

        var callerId = callerAlumnusId is { } id ? new AlumnusId(id) : (AlumnusId?)null;
        var items = await alumni.SearchDirectoryAsync(filter, bypassPrivacy, callerId, skip, take, cancellationToken).ConfigureAwait(false);

        var entries = items.Select(a =>
        {
            var revealHidden = bypassPrivacy || a.Id == callerId;
            return new AlumniDirectoryEntryDto(
                a.Id.Value,
                a.GraduationYear,
                a.ProgramId,
                a.DepartmentId,
                revealHidden || !a.HideCurrentEmployer ? a.CurrentEmployer : null,
                a.Location,
                revealHidden || !a.HideContactDetails ? a.ContactEmail : null,
                revealHidden || !a.HideContactDetails ? a.ContactPhone : null);
        }).ToList();

        return new AlumniDirectoryPage(entries, skip, take);
    }
}
