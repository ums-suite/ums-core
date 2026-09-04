namespace UMS.Modules.Documents.Api.Contracts;

/// <summary>DOC-3: deliberately a single object, never an array - see <c>GenerateDocumentCommand</c>'s own remarks on why this shape alone enforces ADR-0010's sync-path single-document rule.</summary>
public sealed record GenerateDocumentRequestBody(
    Guid OwnerId,
    string DocumentType,
    Guid SourceReferenceId,
    IReadOnlyDictionary<string, string> Fields,
    string? Language);
