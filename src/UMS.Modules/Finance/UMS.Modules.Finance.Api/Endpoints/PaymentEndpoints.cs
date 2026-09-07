using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Finance.Application.Payments;
using UMS.Modules.Finance.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Finance.Api.Endpoints;

/// <summary>FIN-6/FIN-7/FIN-8: requirement-spec.md §6's Payment rows.</summary>
internal static class PaymentEndpoints
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";
    private const string SignatureHeader = "X-Signature";

    public static void MapPaymentEndpoints(this RouteGroupBuilder group)
    {
        var payments = group.MapGroup("/payments");

        payments.MapPost("/", async (InitiatePaymentHttpRequest body, HttpContext httpContext, PaymentService service, CancellationToken cancellationToken) =>
        {
            var idempotencyKey = httpContext.Request.Headers[IdempotencyKeyHeader].ToString();
            var result = await service.InitiateAsync(httpContext.User.GetUserId(), new InitiatePaymentRequest(body.InvoiceId, idempotencyKey), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(FinancePermissions.PaymentInitiate).RequireRateLimiting("finance-payment-initiate");

        payments.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, PaymentService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        // ADR-0008: signature-verified before any state transition. Unauthenticated by design - a
        // real gateway calls this from outside the platform with no JWT, matching Notifications'
        // own provider-webhook convention (WebhookEndpoints' own remarks).
        payments.MapPost("/{id:guid}/gateway-webhook", async (Guid id, HttpContext httpContext, PaymentWebhookService service, CancellationToken cancellationToken) =>
        {
            httpContext.Request.EnableBuffering();
            using var reader = new StreamReader(httpContext.Request.Body, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

            var signature = httpContext.Request.Headers[SignatureHeader].ToString();
            var result = await service.HandleAsync(id, rawBody, signature, httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);

            // ADR-0008/edge-cases.md: every genuine no-op (unknown Payment, duplicate, out-of-order)
            // still returns 200 to the gateway - only a signature failure is ever rejected, so a
            // legitimate gateway never sees a status it would reasonably retry-forever on.
            return result.IsSuccess ? Results.Ok() : result.Error!.ToProblemResult(httpContext);
        }).AllowAnonymous();
    }

    private sealed record InitiatePaymentHttpRequest(Guid InvoiceId);
}
