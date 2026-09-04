using UMS.Modules.Audit.Domain.Exports;

namespace UMS.Modules.Audit.Application.Exports;

public sealed record AuditExportRequestDto(
    Guid Id,
    string Status,
    string Format,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt,
    string? DownloadUrl,
    string? ErrorMessage)
{
    public static AuditExportRequestDto FromDomain(AuditExportRequest request, string? downloadUrl) => new(
        request.Id,
        request.Status.ToString(),
        request.Format.ToString(),
        request.RequestedAt,
        request.CompletedAt,
        downloadUrl,
        request.ErrorMessage);
}
