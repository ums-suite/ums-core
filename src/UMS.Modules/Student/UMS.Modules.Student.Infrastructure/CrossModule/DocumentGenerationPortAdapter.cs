using UMS.Modules.Student.Application.Abstractions;
using UMS.Shared.Documents;

namespace UMS.Modules.Student.Infrastructure.CrossModule;

/// <summary>STU-3: adapts <c>UMS.Shared.Documents.IDocumentGenerationRequester</c> to Student's own local port.</summary>
internal sealed class DocumentGenerationPortAdapter(IDocumentGenerationRequester documentGenerationRequester) : IDocumentGenerationPort
{
    public async Task<DocumentGenerationOutcome> RequestIdCardAsync(RequestStudentIdCardRequest request, CancellationToken cancellationToken = default)
    {
        var command = new RequestDocumentGenerationCommand(
            OwnerId: request.StudentId,
            DocumentType: "IdCard",
            SourceReferenceId: request.StudentId,
            Fields: new Dictionary<string, string>
            {
                ["studentNumber"] = request.StudentNumber,
                ["displayName"] = request.DisplayName,
            },
            LanguageCode: null,
            RequestedByUserId: request.RequestedByUserId,
            CorrelationId: request.CorrelationId);

        var result = await documentGenerationRequester.RequestAsync(command, cancellationToken).ConfigureAwait(false);
        return result.Match(
            summary => new DocumentGenerationOutcome(true, summary.DocumentId, null),
            error => new DocumentGenerationOutcome(false, null, error.Message));
    }

    /// <summary>
    /// Documents' own idempotency natural key is <c>(OwnerId, DocumentType, SourceReferenceId)</c> -
    /// keyed on the StudentRequest id (not the Student id, unlike the ID-card request above) so a
    /// retried Approve call never generates a second transcript document for the same
    /// StudentRequest, while still allowing a Student to hold multiple, separately-generated
    /// transcript documents across distinct transcript-request StudentRequests over time.
    /// </summary>
    public async Task<DocumentGenerationOutcome> RequestTranscriptAsync(RequestStudentTranscriptRequest request, CancellationToken cancellationToken = default)
    {
        var command = new RequestDocumentGenerationCommand(
            OwnerId: request.StudentId,
            DocumentType: "Transcript",
            SourceReferenceId: request.StudentRequestId,
            Fields: request.Fields,
            LanguageCode: null,
            RequestedByUserId: request.RequestedByUserId,
            CorrelationId: request.CorrelationId);

        var result = await documentGenerationRequester.RequestAsync(command, cancellationToken).ConfigureAwait(false);
        return result.Match(
            summary => new DocumentGenerationOutcome(true, summary.DocumentId, null),
            error => new DocumentGenerationOutcome(false, null, error.Message));
    }
}
