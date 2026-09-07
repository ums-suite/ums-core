namespace UMS.Modules.Student.Application.Abstractions;

/// <summary>STU-3/STU-9/STU-10: Student's own local port onto <c>UMS.Shared.Documents.IDocumentGenerationRequester</c>.</summary>
public interface IDocumentGenerationPort
{
    public Task<DocumentGenerationOutcome> RequestIdCardAsync(RequestStudentIdCardRequest request, CancellationToken cancellationToken = default);

    /// <summary>STU-10: requested at <c>StudentRequest.Approve</c> time, with the Academic transcript rows (<see cref="IAcademicTranscriptPort"/>) already resolved into <paramref name="request"/>'s render fields.</summary>
    public Task<DocumentGenerationOutcome> RequestTranscriptAsync(RequestStudentTranscriptRequest request, CancellationToken cancellationToken = default);
}

public sealed record RequestStudentIdCardRequest(Guid StudentId, string StudentNumber, string DisplayName, Guid? RequestedByUserId, string CorrelationId);

/// <summary><paramref name="StudentRequestId"/> is Documents' own idempotency natural-key source-reference (one transcript document per transcript-request `StudentRequest`, never a re-generated duplicate on retry).</summary>
public sealed record RequestStudentTranscriptRequest(Guid StudentId, Guid StudentRequestId, string DisplayName, IReadOnlyDictionary<string, string> Fields, Guid? RequestedByUserId, string CorrelationId);

/// <summary>Best-effort outcome (see <c>Student.IdCardDocumentId</c>'s own remarks).</summary>
public sealed record DocumentGenerationOutcome(bool Succeeded, Guid? DocumentId, string? FailureReason);
