using UMS.Modules.Student.Domain.BulkImport;

namespace UMS.Modules.Student.Application.Abstractions;

public interface IStudentBulkImportJobRepository
{
    public Task<StudentBulkImportJob?> GetByIdAsync(StudentBulkImportJobId id, CancellationToken cancellationToken = default);

    /// <summary>STU-15's own worker relay - jobs sitting in <see cref="StudentBulkImportJobStatus.Approved"/> or already <see cref="StudentBulkImportJobStatus.Processing"/> (resumed after a crash), oldest first.</summary>
    public Task<IReadOnlyList<StudentBulkImportJobId>> GetActiveAsync(int batchSize, CancellationToken cancellationToken = default);

    public void Add(StudentBulkImportJob job);
}
