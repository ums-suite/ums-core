using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Admission.Application.Applicants;
using UMS.Modules.Admission.Application.Applications;
using UMS.Modules.Admission.Application.Permissions;
using UMS.Modules.Admission.Application.Tests;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Admission.Api.Endpoints;

/// <summary>ADM-9/ADM-10: requirement-spec.md §6's AdmissionTest/admit-card rows.</summary>
internal static class AdmissionTestEndpoints
{
    public static void MapAdmissionTestEndpoints(this RouteGroupBuilder group)
    {
        var tests = group.MapGroup("/tests");

        tests.MapPost("/", async (CreateAdmissionTestRequest body, HttpContext httpContext, AdmissionTestService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AdmissionPermissions.CampaignManage);

        tests.MapGet("/by-campaign/{campaignId:guid}", async (Guid campaignId, HttpContext httpContext, AdmissionTestService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByCampaignIdAsync(campaignId, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        tests.MapPost("/{id:guid}/questions", async (Guid id, AddQuestionRequest body, HttpContext httpContext, AdmissionTestService service, CancellationToken cancellationToken) =>
        {
            var result = await service.AddQuestionAsync(id, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AdmissionPermissions.CampaignManage);

        tests.MapPost("/{id:guid}/selection-rules", async (Guid id, SelectionRuleRequest body, HttpContext httpContext, AdmissionTestService service, CancellationToken cancellationToken) =>
        {
            var result = await service.SetSelectionRuleAsync(id, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AdmissionPermissions.CampaignManage);

        tests.MapPost("/{id:guid}/slots", async (Guid id, AddTestSlotRequest body, HttpContext httpContext, AdmissionTestService service, CancellationToken cancellationToken) =>
        {
            var result = await service.AddTestSlotAsync(id, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AdmissionPermissions.CampaignManage);

        group.MapGet("/admit-card/{applicationId:guid}", async (Guid applicationId, HttpContext httpContext, ApplicantService applicantService, ApplicationService applicationService, CancellationToken cancellationToken) =>
        {
            var application = await applicationService.GetByIdAsync(applicationId, cancellationToken).ConfigureAwait(false);
            if (application.IsFailure)
            {
                return application.Error!.ToProblemResult(httpContext);
            }

            var ownApplicantId = await OwnershipGuard.ResolveOwnApplicantIdAsync(applicantService, httpContext, cancellationToken).ConfigureAwait(false);
            if (ownApplicantId.IsFailure)
            {
                return ownApplicantId.Error!.ToProblemResult(httpContext);
            }

            var forbidden = OwnershipGuard.CheckOwnership(ownApplicantId.Value, application.Value.ApplicantId, httpContext);
            return forbidden ?? Results.Ok(new
            {
                application.Value.AssignedTestSlotId,
                application.Value.RollNumber,
                application.Value.AdmitCardDocumentId,
            });
        }).RequireLiveSession();
    }
}
