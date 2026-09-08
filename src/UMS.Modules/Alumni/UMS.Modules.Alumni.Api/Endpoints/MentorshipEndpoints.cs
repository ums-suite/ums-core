using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Alumni.Application.Common;
using UMS.Modules.Alumni.Application.Mentorship;
using UMS.Modules.Alumni.Application.Permissions;
using UMS.Modules.Alumni.Domain.Mentorship;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Student;

namespace UMS.Modules.Alumni.Api.Endpoints;

/// <summary>ALM-11/ALM-12/ALM-13: requirement-spec.md §6 <c>/alumni/mentorship/...</c> rows.</summary>
internal static class MentorshipEndpoints
{
    public static void MapMentorshipEndpoints(this RouteGroupBuilder group)
    {
        var mentorship = group.MapGroup("/mentorship");

        // requirement-spec.md §2.5: an Alumnus opts in as a mentor, a current Student opts in as a mentee.
        mentorship.MapPost("/opt-in", async (OptInRequest body, HttpContext httpContext, MentorshipOptInService service, CallerAlumnusResolver alumnusResolver, IStudentStatusChecker studentStatusChecker, CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<MentorshipRole>(body.Role, ignoreCase: true, out var role))
            {
                return Error.Validation("mentorshipoptin.invalid_role", $"'{body.Role}' is not a recognized MentorshipRole.").ToProblemResult(httpContext);
            }

            Guid personId;
            if (role == MentorshipRole.Mentor)
            {
                var alumnus = await alumnusResolver.ResolveAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
                if (alumnus is null)
                {
                    return Error.Forbidden("mentorshipoptin.not_an_alumnus", "Only an Alumnus may opt in as a mentor.").ToProblemResult(httpContext);
                }

                personId = alumnus.Id.Value;
            }
            else
            {
                var standing = await studentStatusChecker.GetByUserIdAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
                if (standing is null)
                {
                    return Error.Forbidden("mentorshipoptin.not_a_student", "Only a current Student may opt in as a mentee.").ToProblemResult(httpContext);
                }

                personId = standing.StudentId;
            }

            var result = await service.OptInAsync(personId, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        // requirement-spec.md §2.5/§9: matching is Admin/coordinator-initiated in v1.
        mentorship.MapPost("/matches", async (ProposeMatchRequest body, HttpContext httpContext, MentorshipMatchService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ProposeAsync(body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(dto => Results.Created($"/api/v1/alumni/mentorship/matches/{dto.Id}", dto), error => error.ToProblemResult(httpContext));
        }).RequirePermission(AlumniPermissions.MentorshipCoordinate);

        // Lists the caller's own matches, as either mentor or mentee (whichever role resolves).
        mentorship.MapGet("/matches", async (HttpContext httpContext, MentorshipMatchService service, CallerAlumnusResolver alumnusResolver, IStudentStatusChecker studentStatusChecker, CancellationToken cancellationToken) =>
        {
            var alumnus = await alumnusResolver.ResolveAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            if (alumnus is not null)
            {
                return Results.Ok(await service.ListByMentorAsync(alumnus.Id.Value, cancellationToken).ConfigureAwait(false));
            }

            var standing = await studentStatusChecker.GetByUserIdAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            return standing is null
                ? Results.Ok(Array.Empty<MentorshipMatchDto>())
                : Results.Ok(await service.ListByMenteeAsync(standing.StudentId, cancellationToken).ConfigureAwait(false));
        }).RequireLiveSession();

        mentorship.MapPost("/matches/{id:guid}/accept", async (Guid id, HttpContext httpContext, MentorshipMatchService service, CallerAlumnusResolver alumnusResolver, IStudentStatusChecker studentStatusChecker, CancellationToken cancellationToken) =>
        {
            var existing = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (existing.IsFailure)
            {
                return existing.Error!.ToProblemResult(httpContext);
            }

            var alumnus = await alumnusResolver.ResolveAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            if (alumnus is not null && alumnus.Id.Value == existing.Value.MentorAlumnusId)
            {
                var mentorResult = await service.AcceptByMentorAsync(id, cancellationToken).ConfigureAwait(false);
                return mentorResult.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
            }

            var standing = await studentStatusChecker.GetByUserIdAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            if (standing is not null && standing.StudentId == existing.Value.MenteeStudentId)
            {
                var menteeResult = await service.AcceptByMenteeAsync(id, cancellationToken).ConfigureAwait(false);
                return menteeResult.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
            }

            return Error.Forbidden("mentorshipmatch.not_a_party", "Only the proposed mentor or mentee may accept this MentorshipMatch.").ToProblemResult(httpContext);
        }).RequireLiveSession();

        // A coordinator rejects a still-Proposed match.
        mentorship.MapPost("/matches/{id:guid}/reject", async (Guid id, EndMatchRequest body, HttpContext httpContext, MentorshipMatchService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RejectAsync(id, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AlumniPermissions.MentorshipCoordinate);

        // edge-cases.md "A mentor withdraws mid-match": either side may end an Active match.
        mentorship.MapPost("/matches/{id:guid}/end", async (Guid id, EndMatchRequest body, HttpContext httpContext, MentorshipMatchService service, CallerAlumnusResolver alumnusResolver, IStudentStatusChecker studentStatusChecker, CancellationToken cancellationToken) =>
        {
            var existing = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (existing.IsFailure)
            {
                return existing.Error!.ToProblemResult(httpContext);
            }

            var alumnus = await alumnusResolver.ResolveAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            var standing = await studentStatusChecker.GetByUserIdAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            var isParty = (alumnus is not null && alumnus.Id.Value == existing.Value.MentorAlumnusId) || (standing is not null && standing.StudentId == existing.Value.MenteeStudentId);
            if (!isParty)
            {
                return Error.Forbidden("mentorshipmatch.not_a_party", "Only the mentor or mentee may end this MentorshipMatch.").ToProblemResult(httpContext);
            }

            var result = await service.EndAsync(id, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();
    }
}
