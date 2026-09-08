using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Application.Donations;
using UMS.Modules.Alumni.IntegrationTests.Infrastructure;

namespace UMS.Modules.Alumni.IntegrationTests.Donations;

/// <summary>
/// ALM-9/ALM-10: design-decisions.md "Donation Confirmation Consistency Model" - a Donation reaches
/// Confirmed ONLY via Finance's own outbox signal, consumed through the real
/// <see cref="IFinancePaymentEventSource"/> against a hand-crafted <c>finance.outbox_messages</c> row
/// (the real wire shape, not a shortcut call straight into the application service).
/// </summary>
[Collection(AlumniApiTestCollectionDefinition.Name)]
public sealed class DonationConfirmationTests(AlumniServiceFixture fixture)
{
    [Fact]
    public async Task PaymentSucceeded_from_Finances_outbox_confirms_the_matching_Donation()
    {
        using var scope = fixture.Services.CreateScope();
        var campaignService = scope.ServiceProvider.GetRequiredService<DonationCampaignService>();
        var donationService = scope.ServiceProvider.GetRequiredService<DonationService>();
        var confirmationService = scope.ServiceProvider.GetRequiredService<DonationConfirmationService>();
        var financeEventSource = scope.ServiceProvider.GetRequiredService<IFinancePaymentEventSource>();

        var now = DateTimeOffset.UtcNow;
        var campaign = await campaignService.CreateAsync(new CreateDonationCampaignRequest("Scholarship Fund", null, 100_000m, "BDT", now.AddDays(-1), now.AddDays(30)));
        Assert.True(campaign.IsSuccess);

        var alumnusId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var donation = await donationService.InitiateAsync(alumnusId, ownerUserId, new InitiateDonationRequest(campaign.Value.Id, 500m, "BDT", IsAnonymous: false, "None"), "test-correlation");
        Assert.True(donation.IsSuccess);
        Assert.Equal("Pending", donation.Value.Status);
        Assert.NotNull(donation.Value.InvoiceId);

        var eventId = Guid.NewGuid();
        var payload = $$"""{"InvoiceId":"{{donation.Value.InvoiceId}}"}""";
        await fixture.InsertFinanceOutboxMessageAsync(eventId, "PaymentSucceeded", payload, now, now);

        var envelopes = await financeEventSource.GetUnprocessedAsync(10);
        var matched = Assert.Single(envelopes, e => e.InvoiceId == donation.Value.InvoiceId);

        var applied = await confirmationService.ApplyAsync(matched.InvoiceId, matched.EventType, "test-relay");
        Assert.True(applied.IsSuccess);
        Assert.True(applied.Value);

        var reloaded = await donationService.GetByIdAsync(donation.Value.Id);
        Assert.True(reloaded.IsSuccess);
        Assert.Equal("Confirmed", reloaded.Value.Status);
        Assert.NotNull(reloaded.Value.ConfirmedAt);
    }

    /// <summary>design-decisions.md "Recurring-Donation Retry/Dunning Policy" - the FIRST PaymentFailed pauses the schedule immediately, no retry.</summary>
    [Fact]
    public async Task PaymentFailed_on_a_recurring_Donations_first_cycle_pauses_the_schedule()
    {
        using var scope = fixture.Services.CreateScope();
        var campaignService = scope.ServiceProvider.GetRequiredService<DonationCampaignService>();
        var donationService = scope.ServiceProvider.GetRequiredService<DonationService>();
        var confirmationService = scope.ServiceProvider.GetRequiredService<DonationConfirmationService>();

        var now = DateTimeOffset.UtcNow;
        var campaign = await campaignService.CreateAsync(new CreateDonationCampaignRequest("Scholarship Fund", null, 100_000m, "BDT", now.AddDays(-1), now.AddDays(30)));
        Assert.True(campaign.IsSuccess);

        var donation = await donationService.InitiateAsync(Guid.NewGuid(), Guid.NewGuid(), new InitiateDonationRequest(campaign.Value.Id, 500m, "BDT", IsAnonymous: false, "Monthly"), "test-correlation");
        Assert.True(donation.IsSuccess);
        Assert.NotNull(donation.Value.NextChargeAt);

        var applied = await confirmationService.ApplyAsync(donation.Value.InvoiceId!.Value, "PaymentFailed", "test-relay");
        Assert.True(applied.IsSuccess);
        Assert.True(applied.Value);

        var reloaded = await donationService.GetByIdAsync(donation.Value.Id);
        Assert.True(reloaded.IsSuccess);
        Assert.Equal("Failed", reloaded.Value.Status);
        Assert.Equal("Paused", reloaded.Value.RecurrenceStatus);
        Assert.Null(reloaded.Value.NextChargeAt);
    }

    /// <summary>design-decisions.md "Donation-vs-Campaign-Close Consistency Boundary" - checked ONLY at creation; a campaign closing after creation never blocks that Donation's later confirmation.</summary>
    [Fact]
    public async Task A_Donation_created_while_the_campaign_was_active_still_confirms_after_the_campaign_closes_early()
    {
        using var scope = fixture.Services.CreateScope();
        var campaignService = scope.ServiceProvider.GetRequiredService<DonationCampaignService>();
        var donationService = scope.ServiceProvider.GetRequiredService<DonationService>();
        var confirmationService = scope.ServiceProvider.GetRequiredService<DonationConfirmationService>();

        var now = DateTimeOffset.UtcNow;
        var campaign = await campaignService.CreateAsync(new CreateDonationCampaignRequest("Scholarship Fund", null, 100_000m, "BDT", now.AddDays(-1), now.AddDays(30)));
        Assert.True(campaign.IsSuccess);

        var donation = await donationService.InitiateAsync(Guid.NewGuid(), Guid.NewGuid(), new InitiateDonationRequest(campaign.Value.Id, 500m, "BDT", IsAnonymous: false, "None"), "test-correlation");
        Assert.True(donation.IsSuccess);

        var closed = await campaignService.CloseEarlyAsync(campaign.Value.Id);
        Assert.True(closed.IsSuccess);
        Assert.True((await campaignService.GetByIdAsync(campaign.Value.Id)).Value.ClosedEarly);

        var applied = await confirmationService.ApplyAsync(donation.Value.InvoiceId!.Value, "PaymentSucceeded", "test-relay");
        Assert.True(applied.IsSuccess);

        var reloaded = await donationService.GetByIdAsync(donation.Value.Id);
        Assert.True(reloaded.IsSuccess);
        Assert.Equal("Confirmed", reloaded.Value.Status);
    }

    /// <summary>requirement-spec.md §2.4 last-but-one bullet: a new Donation cannot be created against a closed/not-yet-open campaign.</summary>
    [Fact]
    public async Task Initiating_a_Donation_against_an_inactive_campaign_is_rejected()
    {
        using var scope = fixture.Services.CreateScope();
        var campaignService = scope.ServiceProvider.GetRequiredService<DonationCampaignService>();
        var donationService = scope.ServiceProvider.GetRequiredService<DonationService>();

        var now = DateTimeOffset.UtcNow;
        var campaign = await campaignService.CreateAsync(new CreateDonationCampaignRequest("Future Fund", null, 100_000m, "BDT", now.AddDays(10), now.AddDays(40)));
        Assert.True(campaign.IsSuccess);

        var donation = await donationService.InitiateAsync(Guid.NewGuid(), Guid.NewGuid(), new InitiateDonationRequest(campaign.Value.Id, 500m, "BDT", IsAnonymous: false, "None"), "test-correlation");

        Assert.True(donation.IsFailure);
        Assert.Equal("donationcampaign.not_active", donation.Error!.Code);
    }
}
