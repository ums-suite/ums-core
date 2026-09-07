using Microsoft.EntityFrameworkCore;
using UMS.Modules.Learning.Application.Abstractions;
using UMS.Modules.Learning.Domain.Assignments;

namespace UMS.Modules.Learning.Infrastructure.Persistence.Repositories;

internal sealed class AssignmentRepository(LearningDbContext context) : IAssignmentRepository
{
    public Task<Assignment?> GetByIdAsync(AssignmentId id, CancellationToken cancellationToken = default) =>
        context.Assignments.Include(a => a.Extensions).FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Assignment>> GetByCourseOfferingAsync(Guid courseOfferingId, CancellationToken cancellationToken = default) =>
        await context.Assignments
            .Include(a => a.Extensions)
            .Where(a => a.CourseOfferingId == courseOfferingId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <summary>LRN-9's window-close sweep. Ordered oldest-first so a persistently-failing Assignment can never starve newer ones out of the batch.</summary>
    public async Task<IReadOnlyList<Assignment>> GetPublishedPastHardCloseAsync(DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default) =>
        await context.Assignments
            .Include(a => a.Extensions)
            .Where(a => a.Status == AssignmentStatus.Published && a.SubmissionWindow.HardCloseAt <= asOf)
            .OrderBy(a => a.SubmissionWindow.HardCloseAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(Assignment assignment) => context.Assignments.Add(assignment);
}
