using Microsoft.AspNetCore.Http;
using UMS.Modules.Admission.Application.Applicants;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Admission.Api;

/// <summary>
/// requirement-spec.md §6's "Applicant-owned" endpoints: resolves the caller's own
/// <c>ApplicantId</c> from their JWT <c>sub</c> and confirms it matches the target resource's own
/// <c>ApplicantId</c> - a caller with a valid session but no Applicant profile of their own (or
/// someone else's) is rejected outright as Forbidden, never a silent 404 that would otherwise leak
/// existence.
/// </summary>
internal static class OwnershipGuard
{
    public static async Task<Result<Guid>> ResolveOwnApplicantIdAsync(ApplicantService applicants, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var applicant = await applicants.GetByIdentityUserIdAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
        return applicant.Match<Result<Guid>>(dto => dto.Id, error => error);
    }

    public static IResult? CheckOwnership(Guid expectedApplicantId, Guid actualApplicantId, HttpContext httpContext) =>
        expectedApplicantId == actualApplicantId
            ? null
            : Error.Forbidden("admission.not_owner", "You do not own this resource.").ToProblemResult(httpContext);
}
