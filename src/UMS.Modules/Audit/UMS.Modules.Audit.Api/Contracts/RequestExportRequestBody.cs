namespace UMS.Modules.Audit.Api.Contracts;

/// <summary>AUD-9: <c>POST /audit/exports</c>' request body - the same combinable filter set `GET /audit/entries` supports, plus the desired output format.</summary>
public sealed record RequestExportRequestBody(
    string? EntityType,
    string? EntityId,
    string? ActorId,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo,
    string? Action,
    string? Application,
    string Format);
