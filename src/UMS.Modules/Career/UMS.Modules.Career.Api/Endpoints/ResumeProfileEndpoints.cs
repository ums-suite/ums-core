using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Career.Application.ResumeProfiles;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.Student;

namespace UMS.Modules.Career.Api.Endpoints;

/// <summary>CAR-5: `ResumeProfile` CRUD, multiple named versions per Student with one `is_default` (requirement-spec.md §2.7, §6 `/career/resume-profiles`) - purely self-service, no dedicated Permission (mirrors Alumni's own self-service posture).</summary>
internal static class ResumeProfileEndpoints
{
    public static void MapResumeProfileEndpoints(this RouteGroupBuilder group)
    {
        var resumeProfiles = group.MapGroup("/resume-profiles");

        resumeProfiles.MapGet("/", async (HttpContext httpContext, ResumeProfileService service, IStudentStatusChecker studentStatusChecker, CancellationToken cancellationToken) =>
        {
            var studentId = await studentStatusChecker.ResolveStudentIdAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            if (studentId.IsFailure)
            {
                return studentId.Error!.ToProblemResult(httpContext);
            }

            return Results.Ok(await service.ListByStudentAsync(studentId.Value, cancellationToken).ConfigureAwait(false));
        }).RequireLiveSession();

        resumeProfiles.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, ResumeProfileService service, IStudentStatusChecker studentStatusChecker, CancellationToken cancellationToken) =>
        {
            var studentId = await studentStatusChecker.ResolveStudentIdAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            if (studentId.IsFailure)
            {
                return studentId.Error!.ToProblemResult(httpContext);
            }

            var result = await service.GetByIdAsync(id, studentId.Value, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        // Step 1 of the upload flow - a presigned-upload slot request against Documents' own IUploadedArtifactRequester.
        resumeProfiles.MapPost("/upload-requests", async (RequestResumeUploadRequest body, HttpContext httpContext, ResumeProfileService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RequestUploadAsync(httpContext.User.GetUserId(), body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        // Step 2 - finalizes a ResumeProfile against an already-confirmed artifact (edge-cases.md "ResumeProfile submission racing storage confirmation").
        resumeProfiles.MapPost("/", async (CreateResumeProfileRequest body, HttpContext httpContext, ResumeProfileService service, IStudentStatusChecker studentStatusChecker, CancellationToken cancellationToken) =>
        {
            var studentId = await studentStatusChecker.ResolveStudentIdAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            if (studentId.IsFailure)
            {
                return studentId.Error!.ToProblemResult(httpContext);
            }

            var result = await service.CreateAsync(studentId.Value, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(dto => Results.Created($"/api/v1/career/resume-profiles/{dto.Id}", dto), error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        resumeProfiles.MapPatch("/{id:guid}", async (Guid id, UpdateResumeProfileRequest body, HttpContext httpContext, ResumeProfileService service, IStudentStatusChecker studentStatusChecker, CancellationToken cancellationToken) =>
        {
            var studentId = await studentStatusChecker.ResolveStudentIdAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            if (studentId.IsFailure)
            {
                return studentId.Error!.ToProblemResult(httpContext);
            }

            var result = await service.UpdateAsync(id, studentId.Value, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        resumeProfiles.MapDelete("/{id:guid}", async (Guid id, HttpContext httpContext, ResumeProfileService service, IStudentStatusChecker studentStatusChecker, CancellationToken cancellationToken) =>
        {
            var studentId = await studentStatusChecker.ResolveStudentIdAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            if (studentId.IsFailure)
            {
                return studentId.Error!.ToProblemResult(httpContext);
            }

            var result = await service.DeleteAsync(id, studentId.Value, cancellationToken).ConfigureAwait(false);
            return result.IsFailure ? result.Error!.ToProblemResult(httpContext) : Results.NoContent();
        }).RequireLiveSession();
    }
}
