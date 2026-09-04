namespace UMS.Modules.Student.Application.Abstractions;

/// <summary>STU-3: Student's own local port onto <c>UMS.Shared.Documents.IDocumentGenerationRequester</c>.</summary>
public interface IDocumentGenerationPort
{
    public Task<DocumentGenerationOutcome> RequestIdCardAsync(RequestStudentIdCardRequest request, CancellationToken cancellationToken = default);
}

public sealed record RequestStudentIdCardRequest(Guid StudentId, string StudentNumber, string DisplayName, Guid? RequestedByUserId, string CorrelationId);

/// <summary>Best-effort outcome (see <c>Student.IdCardDocumentId</c>'s own remarks).</summary>
public sealed record DocumentGenerationOutcome(bool Succeeded, Guid? DocumentId, string? FailureReason);
