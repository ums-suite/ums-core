namespace UMS.Modules.Documents.Api.Contracts;

public sealed record RequestUploadRequestBody(Guid OwnerId, string ArtifactType, string MimeType);
