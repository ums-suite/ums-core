using Microsoft.EntityFrameworkCore;
using UMS.Modules.Student.Application.Abstractions;
using UMS.Modules.Student.Domain.BulkImport;

namespace UMS.Modules.Student.Infrastructure.Persistence.Repositories;

internal sealed class StudentBulkImportJobRepository(StudentDbContext context) : IStudentBulkImportJobRepository
{
    public Task<StudentBulkImportJob?> GetByIdAsync(StudentBulkImportJobId id, CancellationToken cancellationToken = default) =>
        context.StudentBulkImportJobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public async Task<IReadOnlyList<StudentBulkImportJobId>> GetActiveAsync(int batchSize, CancellationToken cancellationToken = default) =>
        await context.StudentBulkImportJobs
            .Where(j => j.Status == StudentBulkImportJobStatus.Approved || j.Status == StudentBulkImportJobStatus.Processing)
            .OrderBy(j => j.CreatedAt)
            .Take(batchSize)
            .Select(j => j.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(StudentBulkImportJob job) => context.StudentBulkImportJobs.Add(job);
}
