using UMS.Modules.Academic.Application.Abstractions;

namespace UMS.Modules.Academic.Application.CourseOfferings;

/// <summary>ACD-4: registration browsing (requirement-spec.md §6 <c>GET /course-offerings?semester=</c>) - an informational read, open to any authenticated caller.</summary>
public sealed class CourseOfferingQueryService(ICourseOfferingRepository offerings)
{
    public async Task<IReadOnlyList<CourseOfferingDto>> ListBySemesterAsync(Guid semesterId, CancellationToken cancellationToken = default)
    {
        var items = await offerings.GetBySemesterAsync(semesterId, cancellationToken).ConfigureAwait(false);
        return items.Select(CourseOfferingService.ToDto).ToList();
    }
}
