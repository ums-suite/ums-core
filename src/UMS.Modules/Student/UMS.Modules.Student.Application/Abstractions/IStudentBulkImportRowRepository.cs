using UMS.Modules.Student.Domain.BulkImport;

namespace UMS.Modules.Student.Application.Abstractions;

public interface IStudentBulkImportRowRepository
{
    /// <summary>The resume/checkpoint query (edge-cases.md's resumability discipline, mirroring Documents' own <c>IBulkGenerationJobItemRepository.GetUnresolvedBatchAsync</c>) - only <see cref="StudentBulkImportRowStatus.Valid"/> rows (never <see cref="StudentBulkImportRowStatus.Invalid"/>, which never reach processing), ordered by <see cref="StudentBulkImportRow.RowNumber"/> for deterministic, resumable ordering.</summary>
    public Task<IReadOnlyList<StudentBulkImportRow>> GetUnresolvedBatchAsync(StudentBulkImportJobId jobId, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>STU-16's own report query - every row, in original file order.</summary>
    public Task<IReadOnlyList<StudentBulkImportRow>> ListByJobAsync(StudentBulkImportJobId jobId, CancellationToken cancellationToken = default);

    public void AddRange(IEnumerable<StudentBulkImportRow> rows);
}
