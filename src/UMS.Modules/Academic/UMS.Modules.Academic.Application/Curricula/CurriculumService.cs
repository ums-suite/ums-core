using System.Text.Json;
using UMS.Modules.Academic.Application.Abstractions;
using UMS.Modules.Academic.Application.Common;
using UMS.Modules.Academic.Domain.Courses;
using UMS.Modules.Academic.Domain.Curricula;
using UMS.Modules.Academic.Domain.Programs;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Academic.Application.Curricula;

/// <summary>ACD-1: Curriculum create/version - a versioned required/elective Course set for a Program (requirement-spec.md §2, §9 decision 6: version fixed per Student at admission time, not re-evaluated here).</summary>
public sealed class CurriculumService(
    ICurriculumRepository curricula,
    IProgramRepository programs,
    ICourseRepository courses,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    public async Task<Result<CurriculumDto>> CreateAsync(CreateCurriculumRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        if (await programs.GetByIdAsync(new ProgramId(request.ProgramId), cancellationToken).ConfigureAwait(false) is null)
        {
            return Error.Validation("curriculum.program_not_found", $"No Program exists with id '{request.ProgramId}'.");
        }

        if (request.Courses.Count > 0)
        {
            var courseIds = request.Courses.Select(c => new CourseId(c.CourseId)).ToList();
            var found = await courses.GetByIdsAsync(courseIds, cancellationToken).ConfigureAwait(false);
            if (found.Count != courseIds.Count)
            {
                return Error.Validation("curriculum.course_not_found", "One or more Course ids do not exist.");
            }
        }

        Curriculum curriculum;
        try
        {
            curriculum = Curriculum.Create(request.ProgramId, request.Version, clock.UtcNow);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Error.Validation("curriculum.invalid_version", ex.Message);
        }

        foreach (var entry in request.Courses)
        {
            curriculum.AddCourse(new CourseId(entry.CourseId), entry.IsRequired);
        }

        curricula.Add(curriculum);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest("Curriculum", curriculum.Id.Value.ToString(), "create", null, JsonSerializer.Serialize(ToDto(curriculum)));
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : ToDto(curriculum);
    }

    public async Task<Result<CurriculumDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var curriculum = await curricula.GetByIdAsync(new CurriculumId(id), cancellationToken).ConfigureAwait(false);
        return curriculum is null ? Error.NotFound("curriculum.not_found", $"No Curriculum exists with id '{id}'.") : ToDto(curriculum);
    }

    internal static CurriculumDto ToDto(Curriculum curriculum) =>
        new(
            curriculum.Id.Value,
            curriculum.ProgramId,
            curriculum.CurriculumVersion,
            curriculum.Courses.Select(c => new CurriculumCourseEntryDto(c.CourseId.Value, c.IsRequired)).ToList(),
            curriculum.CreatedAt);
}
