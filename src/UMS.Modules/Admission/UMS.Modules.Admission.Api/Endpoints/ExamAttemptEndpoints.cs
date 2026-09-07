using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Admission.Application.Applicants;
using UMS.Modules.Admission.Application.Applications;
using UMS.Modules.Admission.Application.ExamAttempts;
using UMS.Modules.Admission.Application.Permissions;
using UMS.Modules.Admission.Application.Tests;
using UMS.Modules.Admission.Domain.ExamAttempts;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Admission.Api.Endpoints;

/// <summary>ADM-11..14: requirement-spec.md §6's Admission Test Lifecycle rows.</summary>
internal static class ExamAttemptEndpoints
{
    public static void MapExamAttemptEndpoints(this RouteGroupBuilder group)
    {
        var exams = group.MapGroup("/exams");

        exams.MapPost("/{testId:guid}/attempts/start", async (Guid testId, HttpContext httpContext, ApplicantService applicantService, AdmissionTestService testService, ApplicationService applicationService, ExamAttemptService service, CancellationToken cancellationToken) =>
        {
            var ownApplicantId = await OwnershipGuard.ResolveOwnApplicantIdAsync(applicantService, httpContext, cancellationToken).ConfigureAwait(false);
            if (ownApplicantId.IsFailure)
            {
                return ownApplicantId.Error!.ToProblemResult(httpContext);
            }

            var test = await testService.GetByIdAsync(testId, cancellationToken).ConfigureAwait(false);
            if (test.IsFailure)
            {
                return test.Error!.ToProblemResult(httpContext);
            }

            var application = await applicationService.GetByApplicantAndCampaignAsync(ownApplicantId.Value, test.Value.CampaignId, cancellationToken).ConfigureAwait(false);
            if (application.IsFailure)
            {
                return application.Error!.ToProblemResult(httpContext);
            }

            var result = await service.StartAsync(application.Value.Id, httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        var attempts = group.MapGroup("/exams/attempts");

        attempts.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, ExamAttemptService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        attempts.MapPut("/{id:guid}/answers", async (Guid id, SaveAnswerRequest body, HttpContext httpContext, ExamAttemptService service, CancellationToken cancellationToken) =>
        {
            var result = await service.SaveAnswerAsync(id, body, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok() : result.Error!.ToProblemResult(httpContext);
        }).RequireLiveSession();

        attempts.MapPost("/{id:guid}/submit", async (Guid id, HttpContext httpContext, ExamAttemptService service, CancellationToken cancellationToken) =>
        {
            var result = await service.SubmitAsync(id, httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        attempts.MapPost("/{id:guid}/subjective-score", async (Guid id, SubjectiveScoreHttpRequest body, HttpContext httpContext, ExamAttemptService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RecordSubjectiveScoreAsync(id, body.Score, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok() : result.Error!.ToProblemResult(httpContext);
        }).RequirePermission(AdmissionPermissions.ApplicationReview);

        attempts.MapPost("/{id:guid}/integrity-flags", async (Guid id, RaiseFlagHttpRequest body, HttpContext httpContext, ExamAttemptService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RaiseIntegrityFlagAsync(id, body.AnomalyType, body.Details, body.ConfidenceScore, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok() : result.Error!.ToProblemResult(httpContext);
        }).RequireLiveSession();

        attempts.MapPost("/{id:guid}/integrity-flags/{flagId:guid}/review", async (Guid id, Guid flagId, ReviewFlagHttpRequest body, HttpContext httpContext, ExamAttemptService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ReviewIntegrityFlagAsync(id, flagId, body.Outcome, httpContext.User.GetUserId(), body.Notes, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok() : result.Error!.ToProblemResult(httpContext);
        }).RequirePermission(AdmissionPermissions.ApplicationReview);
    }

    private sealed record SubjectiveScoreHttpRequest(decimal Score);

    private sealed record RaiseFlagHttpRequest(string AnomalyType, string Details, decimal ConfidenceScore);

    private sealed record ReviewFlagHttpRequest(IntegrityFlagOutcome Outcome, string? Notes);
}
