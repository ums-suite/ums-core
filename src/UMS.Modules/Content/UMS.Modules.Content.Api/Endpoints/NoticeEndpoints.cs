using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Content.Api.Contracts;
using UMS.Modules.Content.Application.Common;
using UMS.Modules.Content.Application.Notices;
using UMS.Modules.Content.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Content.Api.Endpoints;

/// <summary>CNT-1/2/3/5/6/12/15: requirement-spec.md §6 `/content/notices` rows.</summary>
internal static class NoticeEndpoints
{
    public static void MapNoticeEndpoints(this RouteGroupBuilder group)
    {
        var notices = group.MapGroup("/notices");

        // requirement-spec.md §6: public list (audience=public, published only) - no authentication
        // required. CNT-12's authenticated audience feed is a DISTINCT query path
        // (`?audience=student|faculty|admin`), gated by content.notice.read, never silently folded
        // into this same anonymous path.
        notices.MapGet("/", async (string? audience, int skip, int take, HttpContext httpContext, NoticeService service, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(audience) || string.Equals(audience, "public", StringComparison.OrdinalIgnoreCase))
            {
                var page = await service.ListPublicAsync(httpContext.GetPreferredLanguage(), skip, take, cancellationToken).ConfigureAwait(false);
                return Results.Ok(page);
            }

            return Error.Forbidden("notice.audience_requires_auth", "A non-public audience feed requires authentication - see the authenticated /notices?audience=... route.").ToProblemResult(httpContext);
        });

        // CNT-12: `GET /content/notices?audience=student|faculty|admin` (authenticated).
        notices.MapGet("/feed", async (string audience, int skip, int take, HttpContext httpContext, NoticeService service, CancellationToken cancellationToken) =>
        {
            var parsedAudience = AudienceParser.ParseSingle(audience);
            if (parsedAudience is null)
            {
                return Error.Validation("audience.invalid", $"'{audience}' is not a recognized audience (expected Student, Faculty, or Admin).").ToProblemResult(httpContext);
            }

            var page = await service.ListForAudienceAsync(parsedAudience.Value, httpContext.User.GetUserId(), httpContext.GetPreferredLanguage(), skip, take, cancellationToken).ConfigureAwait(false);
            return Results.Ok(page);
        }).RequirePermission(ContentPermissions.NoticeRead);

        notices.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, NoticeService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, httpContext.GetPreferredLanguage(), httpContext.GetOptionalUserId(), cancellationToken).ConfigureAwait(false);

            // design-decisions.md "Cache-Correctness Backstop": a bare 410, not the normal
            // ProblemDetails 404 shape - origin correctness for an Archived Notice is
            // unconditional, independent of any cache purge outcome.
            return result.Match<IResult>(
                Results.Ok,
                error => error.Code == "notice.archived" ? Results.StatusCode(StatusCodes.Status410Gone) : error.ToProblemResult(httpContext));
        });

        notices.MapPost("/", async (CreateNoticeRequest request, HttpContext httpContext, NoticeService service, CancellationToken cancellationToken) =>
        {
            var audience = AudienceParser.Parse(request.Audience);
            if (audience.IsFailure)
            {
                return audience.Error!.ToProblemResult(httpContext);
            }

            var result = await service.CreateAsync(request.Title, request.Body, audience.Value, request.OrganizationNodeId, request.IsUrgent, httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(dto => Results.Created($"/api/v1/content/notices/{dto.Id}", dto), error => error.ToProblemResult(httpContext));
        }).RequirePermission(ContentPermissions.NoticeWrite);

        notices.MapPut("/{id:guid}", async (Guid id, EditNoticeContentRequest request, HttpContext httpContext, NoticeService service, CancellationToken cancellationToken) =>
        {
            var audit = new AuditContext(httpContext.User.GetUserId(), httpContext.Connection.RemoteIpAddress?.ToString(), httpContext.GetCorrelationId());
            var result = await service.EditContentAsync(id, request.Title, request.Body, audit, request.Version, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ContentPermissions.NoticeWrite);

        // CNT-2: upsert one (languageCode) translation row.
        notices.MapPost("/{id:guid}/translations", async (Guid id, UpsertNoticeTranslationRequest request, HttpContext httpContext, NoticeService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpsertTranslationAsync(id, request.LanguageCode, request.Title, request.Body, httpContext.User.GetUserId(), request.Version, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ContentPermissions.NoticeWrite);

        notices.MapPut("/{id:guid}/schedule", async (Guid id, UpdateNoticeScheduleRequest request, HttpContext httpContext, NoticeService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateScheduleAsync(id, request.PublishAt, request.ExpireAt, request.Version, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ContentPermissions.NoticeWrite);

        // CNT-1/CNT-2: `Draft -> Scheduled` - first bilingual-completeness-gate call site.
        notices.MapPost("/{id:guid}/schedule", async (Guid id, NoticeVersionedActionRequest request, HttpContext httpContext, NoticeService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ScheduleAsync(id, request.Version, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ContentPermissions.NoticeWrite);

        // requirement-spec.md §6/§8: manual immediate publish, bypassing the schedule - second
        // bilingual-completeness-gate call site (identical to the scheduled job's own third).
        notices.MapPost("/{id:guid}/publish", async (Guid id, NoticeVersionedActionRequest request, HttpContext httpContext, NoticeService service, CancellationToken cancellationToken) =>
        {
            var audit = new AuditContext(httpContext.User.GetUserId(), httpContext.Connection.RemoteIpAddress?.ToString(), httpContext.GetCorrelationId());
            var result = await service.PublishAsync(id, audit, request.Version, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ContentPermissions.NoticePublish);

        notices.MapPost("/{id:guid}/archive", async (Guid id, NoticeVersionedActionRequest request, HttpContext httpContext, NoticeService service, CancellationToken cancellationToken) =>
        {
            var audit = new AuditContext(httpContext.User.GetUserId(), httpContext.Connection.RemoteIpAddress?.ToString(), httpContext.GetCorrelationId());
            var result = await service.ArchiveAsync(id, audit, request.Version, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(ContentPermissions.NoticeArchive);
    }
}
