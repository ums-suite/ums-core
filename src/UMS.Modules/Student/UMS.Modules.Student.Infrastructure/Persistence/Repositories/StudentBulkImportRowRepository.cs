using Microsoft.EntityFrameworkCore;
using UMS.Modules.Student.Application.Abstractions;
using UMS.Modules.Student.Domain.BulkImport;

namespace UMS.Modules.Student.Infrastructure.Persistence.Repositories;

internal sealed class StudentBulkImportRowRepository(StudentDbContext context) : IStudentBulkImportRowRepository
{
    public async Task<IReadOnlyList<StudentBulkImportRow>> GetUnresolvedBatchAsync(StudentBulkImportJobId jobId, int batchSize, CancellationToken cancellationToken = default) =>
        await context.StudentBulkImportRows
            .Where(r => r.JobId == jobId && r.Status == StudentBulkImportRowStatus.Valid)
            .OrderBy(r => r.RowNumber)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<StudentBulkImportRow>> ListByJobAsync(StudentBulkImportJobId jobId, CancellationToken cancellationToken = default) =>
        await context.StudentBulkImportRows
            .Where(r => r.JobId == jobId)
            .OrderBy(r => r.RowNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void AddRange(IEnumerable<StudentBulkImportRow> rows) => context.StudentBulkImportRows.AddRange(rows);
}
