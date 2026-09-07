using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Admission.Application.Permissions;
using UMS.Modules.Admission.Application.Results;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Admission.Api.Endpoints;

/// <summary>ADM-17..19/22: requirement-spec.md §6's Result Publication rows. The literal single <c>POST /results/{campaignId}/publish</c> is split into calculate/lock/approve/publish sub-steps - see <see cref="AdmissionResultService"/>'s own remarks for why.</summary>
internal static class ResultEndpoints
{
    public static void MapResultEndpoints(this RouteGroupBuilder group)
    {
        var results = group.MapGroup("/results");

        results.MapPost("/{campaignId:guid}/calculate", async (Guid campaignId, HttpContext httpContext, AdmissionResultService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CalculateAsync(campaignId, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AdmissionPermissions.ResultPublish);

        results.MapPost("/{id:guid}/lock", async (Guid id, HttpContext httpContext, AdmissionResultService service, CancellationToken cancellationToken) =>
        {
            var result = await service.LockAsync(id, httpContext.User.GetUserId(), httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AdmissionPermissions.ResultPublish);

        results.MapPost("/{id:guid}/approve", async (Guid id, HttpContext httpContext, AdmissionResultService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ApproveAsync(id, httpContext.User.GetUserId(), httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AdmissionPermissions.ResultPublish);

        // ADR-0007: triggers the write-through publish. Not yet externally Published on return - see
        // AdmissionResultService.StartPublishingAsync's own remarks; the PublishJob background worker
        // completes it.
        results.MapPost("/{id:guid}/publish", async (Guid id, HttpContext httpContext, AdmissionResultService service, CancellationToken cancellationToken) =>
        {
            var result = await service.StartPublishingAsync(id, httpContext.User.GetUserId(), httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AdmissionPermissions.ResultPublish);

        results.MapPost("/{id:guid}/reenter-for-correction", async (Guid id, HttpContext httpContext, AdmissionResultService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ReenterForCorrectionAsync(id, httpContext.User.GetUserId(), httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AdmissionPermissions.ResultPublish);

        results.MapGet("/by-campaign/{campaignId:guid}", async (Guid campaignId, HttpContext httpContext, AdmissionResultService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByCampaignIdAsync(campaignId, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AdmissionPermissions.ApplicationReview);

        // requirement-spec.md §6: public, rate-limited; served exclusively from Redis (ADR-0007).
        results.MapGet("/search", async (string? applicationNumber, Guid? examId, string? rollNumber, HttpContext httpContext, ResultSearchService service, CancellationToken cancellationToken) =>
        {
            if (!string.IsNullOrWhiteSpace(applicationNumber))
            {
                var byNumber = await service.SearchByApplicationNumberAsync(applicationNumber, cancellationToken).ConfigureAwait(false);
                return byNumber.Match<IResult>(json => Results.Content(json, "application/json"), error => error.ToProblemResult(httpContext));
            }

            if (examId is { } id && !string.IsNullOrWhiteSpace(rollNumber))
            {
                var byRoll = await service.SearchByExamRollNumberAsync(id, rollNumber, cancellationToken).ConfigureAwait(false);
                return byRoll.Match<IResult>(json => Results.Content(json, "application/json"), error => error.ToProblemResult(httpContext));
            }

            return Results.BadRequest("Provide either applicationNumber, or both examId and rollNumber.");
        }).AllowAnonymous().RequireRateLimiting("admission-result-search");

        results.MapPost("/{campaignId:guid}/promote-waitlisted", async (Guid campaignId, PromoteWaitlistedHttpRequest body, HttpContext httpContext, WaitlistPromotionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.PromoteAsync(campaignId, body.ApplicantId, body.ProgramId, httpContext.User.GetUserId(), httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok() : result.Error!.ToProblemResult(httpContext);
        }).RequirePermission(AdmissionPermissions.MeritListApprove);
    }

    private sealed record PromoteWaitlistedHttpRequest(Guid ApplicantId, Guid ProgramId);
}
