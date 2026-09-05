using System.Text.Json;
using UMS.Modules.Academic.Application.Abstractions;
using UMS.Modules.Academic.Application.Common;
using UMS.Modules.Academic.Domain.Common;
using UMS.Modules.Academic.Domain.Courses;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Academic.Application.Courses;

/// <summary>ACD-2: Course create/manage, catalog-level code/title/credit and prerequisites (requirement-spec.md §2).</summary>
public sealed class CourseService(ICourseRepository courses, IUnitOfWork unitOfWork, IAuditRecorder auditRecorder, IClock clock)
{
    public async Task<Result<CourseDto>> CreateAsync(CreateCourseRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var creditHoursResult = CreditHours.Create(request.CreditHours);
        if (creditHoursResult.IsFailure)
        {
            return creditHoursResult.Error!;
        }

        Course course;
        try
        {
            course = Course.Create(request.Code, request.Title, creditHoursResult.Value, clock.UtcNow);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("course.invalid", ex.Message);
        }

        if (request.PrerequisiteCourseIds is { Count: > 0 } prerequisiteIds)
        {
            var prerequisites = await courses.GetByIdsAsync(prerequisiteIds.Select(id => new CourseId(id)).ToList(), cancellationToken).ConfigureAwait(false);
            if (prerequisites.Count != prerequisiteIds.Count)
            {
                return Error.Validation("course.prerequisite_not_found", "One or more prerequisite Course ids do not exist.");
            }

            foreach (var prerequisiteId in prerequisiteIds)
            {
                course.AddPrerequisite(new CourseId(prerequisiteId));
            }
        }

        courses.Add(course);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest("Course", course.Id.Value.ToString(), "create", null, JsonSerializer.Serialize(ToDto(course)));
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : ToDto(course);
    }

    public async Task<Result<CourseDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var course = await courses.GetByIdAsync(new CourseId(id), cancellationToken).ConfigureAwait(false);
        return course is null ? Error.NotFound("course.not_found", $"No Course exists with id '{id}'.") : ToDto(course);
    }

    internal static CourseDto ToDto(Course course) =>
        new(course.Id.Value, course.Code, course.Title, course.CreditHours.Value, course.Prerequisites.Select(p => p.PrerequisiteCourseId.Value).ToList(), course.CreatedAt);
}
