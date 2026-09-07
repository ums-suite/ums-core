using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Admission.Application.MeritLists;
using UMS.Modules.Admission.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Admission.Api.Endpoints;

/// <summary>ADM-15/16/22: requirement-spec.md §6's MeritList rows.</summary>
internal static class MeritListEndpoints
{
    public static void MapMeritListEndpoints(this RouteGroupBuilder group)
    {
        var meritLists = group.MapGroup("/merit-lists");

        meritLists.MapPost("/{campaignId:guid}/generate", async (Guid campaignId, HttpContext httpContext, MeritListService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GenerateAsync(campaignId, httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AdmissionPermissions.MeritListGenerate);

        meritLists.MapPost("/{id:guid}/approve", async (Guid id, HttpContext httpContext, MeritListService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ApproveAsync(id, httpContext.User.GetUserId(), httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AdmissionPermissions.MeritListApprove);

        meritLists.MapGet("/by-campaign/{campaignId:guid}", async (Guid campaignId, HttpContext httpContext, MeritListService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByCampaignIdAsync(campaignId, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AdmissionPermissions.ApplicationReview);
    }
}
