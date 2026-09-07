using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Admission.Application.Applicants;
using UMS.Modules.Admission.Application.Applications;
using UMS.Modules.Admission.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Admission.Api.Endpoints;

/// <summary>ADM-4..8/ADM-20/22: requirement-spec.md §6's Application rows.</summary>
internal static class ApplicationEndpoints
{
    public static void MapApplicationEndpoints(this RouteGroupBuilder group)
    {
        var applications = group.MapGroup("/applications");

        applications.MapPost("/", async (CreateApplicationHttpRequest body, HttpContext httpContext, ApplicantService applicantService, ApplicationService service, CancellationToken cancellationToken) =>
        {
            var ownApplicantId = await OwnershipGuard.ResolveOwnApplicantIdAsync(applicantService, httpContext, cancellationToken).ConfigureAwait(false);
            if (ownApplicantId.IsFailure)
            {
                return ownApplicantId.Error!.ToProblemResult(httpContext);
            }

            var result = await service.CreateDraftAsync(ownApplicantId.Value, body.CampaignId, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        applications.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, ApplicantService applicantService, ApplicationService service, CancellationToken cancellationToken) =>
        {
            var guarded = await GuardAsync(id, httpContext, applicantService, service, cancellationToken).ConfigureAwait(false);
            return guarded.Forbidden ?? Results.Ok(guarded.Application);
        }).RequireLiveSession();

        applications.MapPut("/{id:guid}", async (Guid id, IReadOnlyCollection<ProgramChoiceRequest> body, HttpContext httpContext, ApplicantService applicantService, ApplicationService service, CancellationToken cancellationToken) =>
        {
            var guarded = await GuardAsync(id, httpContext, applicantService, service, cancellationToken).ConfigureAwait(false);
            if (guarded.Forbidden is not null)
            {
                return guarded.Forbidden;
            }

            var result = await service.ReplaceProgramChoicesAsync(id, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        applications.MapPost("/{id:guid}/documents", async (Guid id, UploadDocumentRequest body, HttpContext httpContext, ApplicantService applicantService, ApplicationService service, CancellationToken cancellationToken) =>
        {
            var guarded = await GuardAsync(id, httpContext, applicantService, service, cancellationToken).ConfigureAwait(false);
            if (guarded.Forbidden is not null)
            {
                return guarded.Forbidden;
            }

            var result = await service.UploadDocumentAsync(id, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        applications.MapPost("/{id:guid}/documents/{documentId:guid}/approve", async (Guid id, Guid documentId, HttpContext httpContext, ApplicationService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ApproveDocumentAsync(id, documentId, httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok() : result.Error!.ToProblemResult(httpContext);
        }).RequirePermission(AdmissionPermissions.ApplicationReview);

        applications.MapPost("/{id:guid}/documents/{documentId:guid}/request-resubmission", async (Guid id, Guid documentId, ResubmissionHttpRequest body, HttpContext httpContext, ApplicationService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RequestDocumentResubmissionAsync(id, documentId, body.Reason, httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok() : result.Error!.ToProblemResult(httpContext);
        }).RequirePermission(AdmissionPermissions.ApplicationReview);

        applications.MapPost("/{id:guid}/payment", async (Guid id, HttpContext httpContext, ApplicantService applicantService, ApplicationService service, CancellationToken cancellationToken) =>
        {
            var guarded = await GuardAsync(id, httpContext, applicantService, service, cancellationToken).ConfigureAwait(false);
            if (guarded.Forbidden is not null)
            {
                return guarded.Forbidden;
            }

            var result = await service.InitiateApplicationFeePaymentAsync(id, httpContext.User.GetUserId(), httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        applications.MapPost("/{id:guid}/submit", async (Guid id, HttpContext httpContext, ApplicantService applicantService, ApplicationService service, CancellationToken cancellationToken) =>
        {
            var guarded = await GuardAsync(id, httpContext, applicantService, service, cancellationToken).ConfigureAwait(false);
            if (guarded.Forbidden is not null)
            {
                return guarded.Forbidden;
            }

            var result = await service.SubmitAsync(id, httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        applications.MapPost("/{id:guid}/confirm", async (Guid id, HttpContext httpContext, ApplicantService applicantService, ApplicationService service, CancellationToken cancellationToken) =>
        {
            var guarded = await GuardAsync(id, httpContext, applicantService, service, cancellationToken).ConfigureAwait(false);
            if (guarded.Forbidden is not null)
            {
                return guarded.Forbidden;
            }

            var result = await service.ConfirmAsync(id, httpContext.User.GetUserId(), httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        applications.MapPost("/{id:guid}/decline", async (Guid id, HttpContext httpContext, ApplicationService service, CancellationToken cancellationToken) =>
        {
            var result = await service.DeclineAsync(id, httpContext.User.GetUserId(), httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok() : result.Error!.ToProblemResult(httpContext);
        }).RequirePermission(AdmissionPermissions.ApplicationReview);
    }

    private static async Task<(IResult? Forbidden, ApplicationDto? Application)> GuardAsync(Guid applicationId, HttpContext httpContext, ApplicantService applicantService, ApplicationService service, CancellationToken cancellationToken)
    {
        var application = await service.GetByIdAsync(applicationId, cancellationToken).ConfigureAwait(false);
        if (application.IsFailure)
        {
            return (application.Error!.ToProblemResult(httpContext), null);
        }

        var ownApplicantId = await OwnershipGuard.ResolveOwnApplicantIdAsync(applicantService, httpContext, cancellationToken).ConfigureAwait(false);
        if (ownApplicantId.IsFailure)
        {
            return (ownApplicantId.Error!.ToProblemResult(httpContext), null);
        }

        var forbidden = OwnershipGuard.CheckOwnership(ownApplicantId.Value, application.Value.ApplicantId, httpContext);
        return forbidden is not null ? (forbidden, null) : (null, application.Value);
    }

    private sealed record CreateApplicationHttpRequest(Guid CampaignId);

    private sealed record ResubmissionHttpRequest(string Reason);
}
