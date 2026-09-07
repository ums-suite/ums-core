using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Finance.Application.Invoices;
using UMS.Modules.Finance.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Finance.Api.Endpoints;

/// <summary>
/// FIN-2/FIN-3: requirement-spec.md §6's Invoice rows. <c>POST /</c> is "Inbound API (internal,
/// cross-module)" per §6's own table - gated by <c>finance.invoice.create</c> for a system
/// credential/direct testing; a future in-process caller (Admission) should prefer
/// <c>UMS.Shared.Finance.IInvoiceRequester</c> instead (no HTTP hop) - see that interface's own
/// remarks.
/// </summary>
internal static class InvoiceEndpoints
{
    public static void MapInvoiceEndpoints(this RouteGroupBuilder group)
    {
        var invoices = group.MapGroup("/invoices");

        invoices.MapPost("/", async (CreateInvoiceHttpRequest body, HttpContext httpContext, InvoiceService service, CancellationToken cancellationToken) =>
        {
            var correlationId = string.IsNullOrWhiteSpace(body.CorrelationId) ? httpContext.GetCorrelationId() : body.CorrelationId;
            var result = await service.CreateAsync(
                new CreateInvoiceRequest(body.SourceModule, body.SourceReferenceId, body.FeeType, body.OwnerId, body.ApplicabilityReferenceId, body.RequestedByUserId, correlationId),
                cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(
                dto => Results.Created($"/api/v1/finance/invoices/{dto.Id}", dto),
                error => error.ToProblemResult(httpContext));
        }).RequirePermission(FinancePermissions.InvoiceCreate);

        invoices.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, InvoiceService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        invoices.MapGet("/", async (Guid ownerId, HttpContext httpContext, InvoiceService service, CancellationToken cancellationToken) =>
        {
            // requirement-spec.md §6: "ownership-scoped per ADR-0006" - a caller may only ever list
            // their own Invoices with this query surface (no cross-owner batch listing here; that is
            // finance.invoice.read's own Accountant/Admin-facing surface, reserved for the remainder
            // Finance pass, Flow #18).
            if (ownerId != httpContext.User.GetUserId())
            {
                return Results.Forbid();
            }

            var result = await service.ListByOwnerAsync(ownerId, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();
    }

    private sealed record CreateInvoiceHttpRequest(
        string SourceModule,
        string SourceReferenceId,
        string FeeType,
        Guid OwnerId,
        Guid? ApplicabilityReferenceId,
        Guid? RequestedByUserId,
        string? CorrelationId);
}
