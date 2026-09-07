using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Admission.Application.Applicants;
using UMS.Modules.Admission.Domain.Applicants;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Admission.Api.Endpoints;

/// <summary>ADM-2/ADM-3: requirement-spec.md §6's Applicant rows.</summary>
internal static class ApplicantEndpoints
{
    public static void MapApplicantEndpoints(this RouteGroupBuilder group)
    {
        var applicants = group.MapGroup("/applicants");

        applicants.MapPost("/", async (RegisterApplicantRequest body, HttpContext httpContext, ApplicantService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RegisterAsync(body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).AllowAnonymous();

        applicants.MapPost("/{id:guid}/otp", async (Guid id, RequestOtpHttpRequest body, HttpContext httpContext, ApplicantService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RequestOtpAsync(id, body.Channel, httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok() : result.Error!.ToProblemResult(httpContext);
        }).AllowAnonymous();

        applicants.MapPost("/{id:guid}/verify", async (Guid id, VerifyOtpHttpRequest body, HttpContext httpContext, ApplicantService service, CancellationToken cancellationToken) =>
        {
            var result = await service.VerifyOtpAsync(id, body.Channel, body.Code, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok() : result.Error!.ToProblemResult(httpContext);
        }).AllowAnonymous();

        applicants.MapGet("/me", async (HttpContext httpContext, ApplicantService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdentityUserIdAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        applicants.MapPost("/{id:guid}/academic-records", async (Guid id, AcademicRecordRequest body, HttpContext httpContext, ApplicantService service, CancellationToken cancellationToken) =>
        {
            var ownApplicantId = await OwnershipGuard.ResolveOwnApplicantIdAsync(service, httpContext, cancellationToken).ConfigureAwait(false);
            if (ownApplicantId.IsFailure)
            {
                return ownApplicantId.Error!.ToProblemResult(httpContext);
            }

            var forbidden = OwnershipGuard.CheckOwnership(ownApplicantId.Value, id, httpContext);
            if (forbidden is not null)
            {
                return forbidden;
            }

            var result = await service.AddAcademicRecordAsync(id, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();
    }

    private sealed record RequestOtpHttpRequest(OtpChannel Channel);

    private sealed record VerifyOtpHttpRequest(OtpChannel Channel, string Code);
}
