using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Documents.Application.Verification;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Documents.Api.Endpoints;

/// <summary>DOC-9: the one deliberately public, unauthenticated endpoint in this module (requirement-spec.md documents §2/§6) - rate-limited via the "document-verify" policy registered at the Host composition root (see <c>Program.cs</c>).</summary>
internal static class VerificationEndpoints
{
    public static void MapVerificationEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/verify/{verificationId}", async (string verificationId, HttpContext httpContext, VerifyDocumentService service, CancellationToken cancellationToken) =>
        {
            var result = await service.VerifyAsync(verificationId, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        })
        .AllowAnonymous()
        .RequireRateLimiting("document-verify");
    }
}
