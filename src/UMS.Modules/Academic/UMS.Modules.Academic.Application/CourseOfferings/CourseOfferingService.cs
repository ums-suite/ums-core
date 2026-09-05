using System.Text.Json;
using UMS.Modules.Academic.Application.Abstractions;
using UMS.Modules.Academic.Application.Common;
using UMS.Modules.Academic.Domain.AcademicSessions;
using UMS.Modules.Academic.Domain.Common;
using UMS.Modules.Academic.Domain.CourseOfferings;
using UMS.Modules.Academic.Domain.Courses;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Faculty;

namespace UMS.Modules.Academic.Application.CourseOfferings;

/// <summary>ACD-3: CourseOffering create/manage - schedules a Course in a Semester with Sections and an Instructor (requirement-spec.md §2, §7 Faculty dependency). Publishes <c>CourseOfferingPublished</c> on create.</summary>
public sealed class CourseOfferingService(
    ICourseOfferingRepository offerings,
    ICourseRepository courses,
    IAcademicSessionRepository sessions,
    IFacultyMemberLookup facultyMemberLookup,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    public async Task<Result<CourseOfferingDto>> CreateAsync(CreateCourseOfferingRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        if (await courses.GetByIdAsync(new CourseId(request.CourseId), cancellationToken).ConfigureAwait(false) is null)
        {
            return Error.Validation("courseoffering.course_not_found", $"No Course exists with id '{request.CourseId}'.");
        }

        if (await sessions.GetSemesterByIdAsync(new SemesterId(request.SemesterId), cancellationToken).ConfigureAwait(false) is null)
        {
            return Error.Validation("courseoffering.semester_not_found", $"No Semester exists with id '{request.SemesterId}'.");
        }

        Domain.CourseOfferings.CourseOffering offering;
        try
        {
            offering = Domain.CourseOfferings.CourseOffering.Create(request.CourseId, request.SemesterId, request.DepartmentId, request.Capacity, clock.UtcNow);
            foreach (var section in request.Sections)
            {
                var scheduleResult = WeeklyTimeSlot.Create(section.DayOfWeek, section.Start, section.End);
                if (scheduleResult.IsFailure)
                {
                    return Error.Validation("courseoffering.invalid_section", scheduleResult.Error!.Message);
                }

                offering.AddSection(section.Code, scheduleResult.Value);
            }
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("courseoffering.invalid", ex.Message);
        }

        offerings.Add(offering);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest("CourseOffering", offering.Id.Value.ToString(), "create", null, JsonSerializer.Serialize(ToDto(offering)), organizationScopeId: request.DepartmentId);
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : ToDto(offering);
    }

    /// <summary>requirement-spec.md §2/§7: the Instructor reference is "resolved via Faculty's application-service interface, never a direct table join" - eligibility (Department match, `Active` status) is validated here, fresh, before the assignment is recorded.</summary>
    public async Task<Result<CourseOfferingDto>> AssignInstructorAsync(Guid id, AssignInstructorRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var offering = await offerings.GetByIdAsync(new CourseOfferingId(id), cancellationToken).ConfigureAwait(false);
        if (offering is null)
        {
            return Error.NotFound("courseoffering.not_found", $"No CourseOffering exists with id '{id}'.");
        }

        var facultyMember = await facultyMemberLookup.GetAsync(request.FacultyMemberId, cancellationToken).ConfigureAwait(false);
        if (facultyMember is null)
        {
            return Error.Validation("courseoffering.instructor_not_found", $"No FacultyMember exists with id '{request.FacultyMemberId}'.");
        }

        if (!string.Equals(facultyMember.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return Error.Validation("courseoffering.instructor_not_active", $"FacultyMember '{request.FacultyMemberId}' is not Active (status: '{facultyMember.Status}') and cannot be assigned as an instructor.");
        }

        var before = offering.InstructorFacultyMemberId;
        offering.AssignInstructor(request.FacultyMemberId, clock.UtcNow);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest(
            "CourseOffering",
            offering.Id.Value.ToString(),
            "assign_instructor",
            JsonSerializer.Serialize(new { instructorFacultyMemberId = before }),
            JsonSerializer.Serialize(new { instructorFacultyMemberId = request.FacultyMemberId }),
            organizationScopeId: offering.DepartmentId);
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : ToDto(offering);
    }

    public async Task<Result<CourseOfferingDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var offering = await offerings.GetByIdAsync(new CourseOfferingId(id), cancellationToken).ConfigureAwait(false);
        return offering is null ? Error.NotFound("courseoffering.not_found", $"No CourseOffering exists with id '{id}'.") : ToDto(offering);
    }

    internal static CourseOfferingDto ToDto(Domain.CourseOfferings.CourseOffering offering) =>
        new(
            offering.Id.Value,
            offering.CourseId,
            offering.SemesterId,
            offering.DepartmentId,
            offering.Capacity,
            offering.EnrolledCount,
            offering.HasAvailableSeats,
            offering.InstructorFacultyMemberId,
            offering.Sections.Select(s => new SectionDto(s.Id.Value, s.Code, s.Schedule.DayOfWeek, s.Schedule.Start, s.Schedule.End)).ToList(),
            offering.Exams.Select(e => new ExamDto(e.Id.Value, e.Name, e.Assessments.Select(a => new AssessmentDto(a.Id.Value, a.Name, a.Weight)).ToList())).ToList(),
            offering.CreatedAt);
}
