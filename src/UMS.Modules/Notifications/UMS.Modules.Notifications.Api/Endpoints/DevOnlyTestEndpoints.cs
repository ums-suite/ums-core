using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Shared.ErrorHandling;
using UMS.Shared.Notifications;

namespace UMS.Modules.Notifications.Api.Endpoints;

/// <summary>
/// requirement-spec.md §9 Decision 1 is explicit that there is no public inbound HTTP endpoint for
/// another module to submit a notification - strictly the in-process
/// <see cref="INotificationRequestIntake"/> path (ADR-0003/0009). This one endpoint is the single,
/// clearly-marked exception: a Development-only test seam (mapped only when
/// <c>IHostEnvironment.IsDevelopment()</c> - see <c>NotificationsModule</c>) that exists purely so a
/// developer/manual-tester can exercise the real fan-out/dedup/opt-out/dispatch pipeline against a
/// running Host without a real publishing module (Admission/Finance/Academic/...) existing yet to
/// call <see cref="INotificationRequestIntake"/> for real (see that interface's own remarks on this
/// exact gap). Never mapped outside Development, and never a substitute for the real per-module
/// outbox-relay call sites those modules will each add once they exist.
/// </summary>
internal static class DevOnlyTestEndpoints
{
    public static void MapDevOnlyTestSubmitEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/_dev/test-submit", async (SubmitNotificationRequestCommand command, INotificationRequestIntake intake, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var result = await intake.SubmitAsync(command, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(id => Results.Ok(new { notificationRequestId = id }), error => error.ToProblemResult(httpContext));
        }).AllowAnonymous();
    }
}
