using Microsoft.AspNetCore.Http;
using UMS.Modules.Hostel.Application.Common;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Hostel.Api;

/// <summary>requirement-spec.md §6: "Every endpoint enforces resource ownership... a valid JWT alone is never sufficient." Mirrors Admission's own <c>OwnershipGuard</c> exactly, resolving a <c>StudentId</c> instead of an <c>ApplicantId</c>.</summary>
internal static class OwnershipGuard
{
    public static async Task<Result<Guid>> ResolveOwnStudentIdAsync(StudentContextService studentContext, HttpContext httpContext, CancellationToken cancellationToken) =>
        await studentContext.ResolveOwnStudentIdAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);

    public static IResult? CheckOwnership(Guid expectedStudentId, Guid actualStudentId, HttpContext httpContext) =>
        expectedStudentId == actualStudentId
            ? null
            : Error.Forbidden("hostel.not_owner", "You do not own this resource.").ToProblemResult(httpContext);
}
