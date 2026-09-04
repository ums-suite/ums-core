using UMS.Modules.Documents.Domain.GeneratedDocuments;

namespace UMS.Modules.Documents.Application.Generation;

public sealed record GeneratedDocumentDto(
    Guid Id,
    Guid OwnerId,
    string DocumentType,
    Guid SourceReferenceId,
    Guid TemplateId,
    int TemplateVersion,
    string Status,
    string DigitalVerificationId,
    string? MimeType,
    long? SizeBytes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? RevokedAt,
    string? RevokedReason,
    Guid? SupersededByDocumentId,
    string? DownloadUrl)
{
    public static GeneratedDocumentDto FromDomain(GeneratedDocument document, string? downloadUrl) => new(
        document.Id.Value,
        document.OwnerId,
        document.DocumentType.ToString(),
        document.SourceReferenceId,
        document.TemplateId.Value,
        document.TemplateVersion,
        document.Status.ToString(),
        document.DigitalVerificationId,
        document.MimeType,
        document.SizeBytes,
        document.CreatedAt,
        document.ReadyAt,
        document.RevokedAt,
        document.RevokedReason,
        document.SupersededByDocumentId?.Value,
        downloadUrl);
}
