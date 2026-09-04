namespace UMS.Modules.Documents.Infrastructure.Storage;

/// <summary>Bound from <c>Documents:Storage</c> - points at the platform's MinIO instance (ums-devops's docker-compose, ADR-0010) in local dev, or any S3-compatible endpoint in a deployed environment. Mirrors Audit's own <c>ObjectStorageOptions</c> exactly.</summary>
public sealed class ObjectStorageOptions
{
    public string Endpoint { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public string Bucket { get; set; } = string.Empty;

    public bool UseHttps { get; set; }
}
