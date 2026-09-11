using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Alumni.Application.Alumni;
using UMS.Modules.Alumni.Application.Donations;
using UMS.Modules.Alumni.Application.Jobs;
using UMS.Modules.Alumni.Application.Mentorship;
using UMS.Modules.Alumni.IntegrationTests.Infrastructure;
using UMS.Shared.Alumni;
using UMS.Shared.Student;

namespace UMS.Modules.Alumni.IntegrationTests.CrossModule;

/// <summary>
/// Flow #31: exercises <c>AlumniReportingQueryAdapter</c> - the one real implementation of
/// <see cref="IAlumniReportingQuery"/> - against a real Postgres row set, mirroring
/// <c>UMS.Modules.Content.IntegrationTests.CrossModule.ContentReportingQueryAdapterTests</c>'s own
/// posture exactly (itself Flow #26's own precedent).
/// </summary>
[Collection(AlumniApiTestCollectionDefinition.Name)]
public sealed class AlumniReportingQueryAdapterTests(AlumniServiceFixture fixture)
{
    [Fact]
    public async Task Snapshot_counts_only_Confirmed_Donations_Published_JobPostings_and_Active_MentorshipMatches()
    {
        using var scope = fixture.Services.CreateScope();
        var alumnusService = scope.ServiceProvider.GetRequiredService<AlumnusService>();
        var campaignService = scope.ServiceProvider.GetRequiredService<DonationCampaignService>();
        var donationService = scope.ServiceProvider.GetRequiredService<DonationService>();
        var confirmationService = scope.ServiceProvider.GetRequiredService<DonationConfirmationService>();
        var jobPostingService = scope.ServiceProvider.GetRequiredService<JobPostingService>();
        var optInService = scope.ServiceProvider.GetRequiredService<MentorshipOptInService>();
        var matchService = scope.ServiceProvider.GetRequiredService<MentorshipMatchService>();
        var query = scope.ServiceProvider.GetRequiredService<IAlumniReportingQuery>();

        // Deltas, not absolute totals - this fixture's Postgres instance is shared across every test
        // in this collection (AlumniApiTestCollectionDefinition).
        var before = await query.GetDashboardSnapshotAsync();
        var beforeBdt = before.ConfirmedDonationAmountByCurrency.GetValueOrDefault("BDT");

        // ALM-1: a Student->Alumnus transition - counts toward TotalAlumnusCount.
        var studentId = Guid.NewGuid();
        fixture.StudentStatusChecker.Register(new StudentAcademicStanding(studentId, Guid.NewGuid(), Guid.NewGuid(), "Graduated"));
        var alumnus = await alumnusService.CreateFromStudentGraduationAsync(studentId, DateTimeOffset.UtcNow);
        Assert.True(alumnus.IsSuccess);

        // A Confirmed Donation counts toward the BDT total; a Pending one (never confirmed) must not.
        var now = DateTimeOffset.UtcNow;
        var campaign = await campaignService.CreateAsync(new CreateDonationCampaignRequest("Scholarship Fund", null, 100_000m, "BDT", now.AddDays(-1), now.AddDays(30)));
        Assert.True(campaign.IsSuccess);

        var confirmedDonation = await donationService.InitiateAsync(alumnus.Value.Id, Guid.NewGuid(), new InitiateDonationRequest(campaign.Value.Id, 500m, "BDT", IsAnonymous: false, "None"), "test-correlation");
        Assert.True(confirmedDonation.IsSuccess);
        var confirmed = await confirmationService.ApplyAsync(confirmedDonation.Value.InvoiceId!.Value, "PaymentSucceeded", "test-relay");
        Assert.True(confirmed.IsSuccess);

        var pendingDonation = await donationService.InitiateAsync(alumnus.Value.Id, Guid.NewGuid(), new InitiateDonationRequest(campaign.Value.Id, 9_999m, "BDT", IsAnonymous: false, "None"), "test-correlation");
        Assert.True(pendingDonation.IsSuccess);

        // An alumnus-posted JobPosting auto-publishes (Published) - counts toward ActiveJobPostingCount.
        var publishedPosting = await jobPostingService.PostAsync(
            Guid.NewGuid(), posterIsAlumnus: true, alumnus.Value.Id,
            new PostJobRequest("Backend Engineer", "Acme", "d", "Dhaka", "email", now.AddDays(30)));
        Assert.True(publishedPosting.IsSuccess);
        Assert.Equal("Published", publishedPosting.Value.Status);

        // A non-alumnus employer's posting stays PendingModeration - must NOT be counted.
        var pendingPosting = await jobPostingService.PostAsync(
            Guid.NewGuid(), posterIsAlumnus: false, posterAlumnusId: null,
            new PostJobRequest("Marketing Intern", "Beta Corp", "d", "Dhaka", "email", now.AddDays(30)));
        Assert.True(pendingPosting.IsSuccess);
        Assert.Equal("PendingModeration", pendingPosting.Value.Status);

        // A two-sided-accepted MentorshipMatch reaches Active - counts toward ActiveMentorshipMatchCount.
        var mentorId = alumnus.Value.Id;
        var menteeId = Guid.NewGuid();
        var mentorOptIn = await optInService.OptInAsync(mentorId, new OptInRequest("Mentor", "Backend", 5, "Weekends"));
        Assert.True(mentorOptIn.IsSuccess);
        var menteeOptIn = await optInService.OptInAsync(menteeId, new OptInRequest("Mentee", "Backend", 1, "Weekends"));
        Assert.True(menteeOptIn.IsSuccess);

        var proposed = await matchService.ProposeAsync(new ProposeMatchRequest(mentorId, menteeId));
        Assert.True(proposed.IsSuccess);
        var acceptedByMentor = await matchService.AcceptByMentorAsync(proposed.Value.Id);
        Assert.True(acceptedByMentor.IsSuccess);
        var acceptedByMentee = await matchService.AcceptByMenteeAsync(proposed.Value.Id);
        Assert.True(acceptedByMentee.IsSuccess);
        Assert.Equal("Active", acceptedByMentee.Value.Status);

        // A still-Proposed match (only one side accepted) must NOT be counted as Active.
        var otherMentorId = Guid.NewGuid();
        var otherMenteeId = Guid.NewGuid();
        var otherMentorOptIn = await optInService.OptInAsync(otherMentorId, new OptInRequest("Mentor", "Frontend", 2, null));
        Assert.True(otherMentorOptIn.IsSuccess);
        var otherMenteeOptIn = await optInService.OptInAsync(otherMenteeId, new OptInRequest("Mentee", "Frontend", 1, null));
        Assert.True(otherMenteeOptIn.IsSuccess);
        var stillProposed = await matchService.ProposeAsync(new ProposeMatchRequest(otherMentorId, otherMenteeId));
        Assert.True(stillProposed.IsSuccess);

        var after = await query.GetDashboardSnapshotAsync();

        Assert.Equal(1, after.TotalAlumnusCount - before.TotalAlumnusCount);
        Assert.Equal(500m, after.ConfirmedDonationAmountByCurrency["BDT"] - beforeBdt);
        Assert.Equal(1, after.ActiveJobPostingCount - before.ActiveJobPostingCount);
        Assert.Equal(1, after.ActiveMentorshipMatchCount - before.ActiveMentorshipMatchCount);
    }
}
