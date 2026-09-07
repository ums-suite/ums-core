using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Admission.Application.Campaigns;
using UMS.Modules.Admission.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Admission.Api.Endpoints;

/// <summary>ADM-1: requirement-spec.md §6's Campaign row.</summary>
internal static class CampaignEndpoints
{
    public static void MapCampaignEndpoints(this RouteGroupBuilder group)
    {
        var campaigns = group.MapGroup("/campaigns");

        campaigns.MapPost("/", async (CreateCampaignRequest body, HttpContext httpContext, CampaignService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AdmissionPermissions.CampaignManage);

        campaigns.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, CampaignService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        campaigns.MapPost("/{id:guid}/eligibility-rules", async (Guid id, EligibilityRuleRequest body, HttpContext httpContext, CampaignService service, CancellationToken cancellationToken) =>
        {
            var result = await service.AddEligibilityRuleAsync(id, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AdmissionPermissions.CampaignManage);

        campaigns.MapPost("/{id:guid}/seat-quotas", async (Guid id, SeatQuotaRequest body, HttpContext httpContext, CampaignService service, CancellationToken cancellationToken) =>
        {
            var result = await service.AddSeatQuotaAsync(id, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AdmissionPermissions.CampaignManage);

        campaigns.MapPost("/{id:guid}/required-documents", async (Guid id, RequiredDocumentHttpRequest body, HttpContext httpContext, CampaignService service, CancellationToken cancellationToken) =>
        {
            var result = await service.AddRequiredDocumentTypeAsync(id, body.DocumentType, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AdmissionPermissions.CampaignManage);
    }

    private sealed record RequiredDocumentHttpRequest(string DocumentType);
}
