using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Alumni.Application.Common;
using UMS.Modules.Alumni.Application.Donations;
using UMS.Modules.Alumni.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Alumni.Api.Endpoints;

/// <summary>ALM-8/ALM-9/ALM-10: requirement-spec.md §6 <c>/alumni/donations</c> rows. Donation campaigns are a lightweight Alumni-owned reference (§2.4) exposed here too, since a Donation cannot be initiated without one.</summary>
internal static class DonationEndpoints
{
    public static void MapDonationEndpoints(this RouteGroupBuilder group)
    {
        var campaigns = group.MapGroup("/donation-campaigns");

        campaigns.MapGet("/", async (int? skip, int? take, DonationCampaignService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(skip ?? 0, take ?? 50, cancellationToken).ConfigureAwait(false))).RequireLiveSession();

        campaigns.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, DonationCampaignService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        campaigns.MapPost("/", async (CreateDonationCampaignRequest body, HttpContext httpContext, DonationCampaignService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(dto => Results.Created($"/api/v1/alumni/donation-campaigns/{dto.Id}", dto), error => error.ToProblemResult(httpContext));
        }).RequirePermission(AlumniPermissions.CampaignManage);

        campaigns.MapPost("/{id:guid}/close-early", async (Guid id, HttpContext httpContext, DonationCampaignService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CloseEarlyAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AlumniPermissions.CampaignManage);

        var donations = group.MapGroup("/donations");

        donations.MapGet("/", async (HttpContext httpContext, int? skip, int? take, DonationService service, CallerAlumnusResolver resolver, CancellationToken cancellationToken) =>
        {
            var alumnus = await resolver.ResolveAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            if (alumnus is null)
            {
                return Results.Ok(Array.Empty<DonationDto>());
            }

            return Results.Ok(await service.ListByAlumnusAsync(alumnus.Id.Value, skip ?? 0, take ?? 50, cancellationToken).ConfigureAwait(false));
        }).RequireLiveSession();

        donations.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, DonationService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        // requirement-spec.md item 9: Alumni delegates 100% of payment processing to Finance - only an Alumnus may initiate a Donation.
        donations.MapPost("/", async (InitiateDonationRequest body, HttpContext httpContext, DonationService service, CallerAlumnusResolver resolver, CancellationToken cancellationToken) =>
        {
            var alumnus = await resolver.ResolveAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            if (alumnus is null)
            {
                return Error.Forbidden("donation.not_an_alumnus", "Only an Alumnus may initiate a Donation.").ToProblemResult(httpContext);
            }

            var result = await service.InitiateAsync(alumnus.Id.Value, httpContext.User.GetUserId(), body, httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(dto => Results.Created($"/api/v1/alumni/donations/{dto.Id}", dto), error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        donations.MapPost("/{id:guid}/cancel-recurring", async (Guid id, HttpContext httpContext, DonationService service, CallerAlumnusResolver resolver, CancellationToken cancellationToken) =>
        {
            var guard = await EnsureOwnDonationAsync(id, httpContext, service, resolver, cancellationToken).ConfigureAwait(false);
            if (guard is not null)
            {
                return guard;
            }

            var result = await service.CancelRecurringAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        // edge-cases.md's own stated "sibling resume path" to cancel-recurring.
        donations.MapPost("/{id:guid}/resume-recurring", async (Guid id, HttpContext httpContext, DonationService service, CallerAlumnusResolver resolver, CancellationToken cancellationToken) =>
        {
            var guard = await EnsureOwnDonationAsync(id, httpContext, service, resolver, cancellationToken).ConfigureAwait(false);
            if (guard is not null)
            {
                return guard;
            }

            var result = await service.ResumeRecurringAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();
    }

    private static async Task<IResult?> EnsureOwnDonationAsync(Guid donationId, HttpContext httpContext, DonationService service, CallerAlumnusResolver resolver, CancellationToken cancellationToken)
    {
        var donation = await service.GetByIdAsync(donationId, cancellationToken).ConfigureAwait(false);
        if (donation.IsFailure)
        {
            return donation.Error!.ToProblemResult(httpContext);
        }

        var alumnus = await resolver.ResolveAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
        return alumnus is not null && alumnus.Id.Value == donation.Value.AlumnusId
            ? null
            : Error.Forbidden("donation.not_owner", "Only the donor may manage this Donation's recurring schedule.").ToProblemResult(httpContext);
    }
}
