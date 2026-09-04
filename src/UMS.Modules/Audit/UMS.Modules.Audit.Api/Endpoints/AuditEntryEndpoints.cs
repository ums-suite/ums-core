using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Audit.Application.Entries;
using UMS.Modules.Audit.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Audit.Api.Endpoints;

/// <summary>AUD-6/7/8: filtered listing, single-entry detail, and entity-history reads (requirement-spec.md audit §6).</summary>
internal static class AuditEntryEndpoints
{
    public static void MapAuditEntryEndpoints(this RouteGroupBuilder group)
    {
        var entries = group.MapGroup("/entries").RequirePermission(AuditPermissions.EntryRead);

        entries.MapGet("/", async (
            string? entityType,
            string? entityId,
            string? actorId,
            DateTimeOffset? dateFrom,
            DateTimeOffset? dateTo,
            string? action,
            string? application,
            int? skip,
            int? take,
            HttpContext httpContext,
            AuditQueryService service,
            CancellationToken cancellationToken) =>
        {
            var filter = new AuditEntryFilter(entityType, entityId, actorId, dateFrom, dateTo, action, application);
            var page = await service.ListAsync(filter, skip ?? 0, take ?? 50, httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            return Results.Ok(page);
        });

        entries.MapGet("/{id}", async (string id, HttpContext httpContext, AuditQueryService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        });

        group.MapGet("/entities/{entityType}/{entityId}/history", async (
            string entityType,
            string entityId,
            HttpContext httpContext,
            AuditQueryService service,
            CancellationToken cancellationToken) =>
        {
            var history = await service.GetEntityHistoryAsync(entityType, entityId, httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            return Results.Ok(history);
        }).RequirePermission(AuditPermissions.EntryRead);
    }
}
