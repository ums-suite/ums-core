using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Career.Application.Abstractions;
using UMS.Modules.Career.Application.Drives;
using UMS.Modules.Career.Application.Internships;
using UMS.Modules.Career.Domain.Applications;
using UMS.Modules.Career.Domain.Drives;
using UMS.Modules.Career.Domain.Internships;
using UMS.Modules.Career.IntegrationTests.Infrastructure;
using UMS.Shared.Career;

namespace UMS.Modules.Career.IntegrationTests.CrossModule;

/// <summary>
/// Flow #31: exercises <c>CareerReportingQueryAdapter</c> - the one real implementation of
/// <see cref="ICareerReportingQuery"/> - against a real Postgres row set, mirroring
/// <c>UMS.Modules.Content.IntegrationTests.CrossModule.ContentReportingQueryAdapterTests</c>'s own
/// posture exactly (itself Flow #26's own precedent). <see cref="CareerApplication"/> rows and an
/// InterviewSlot's own claimed capacity are written directly through their own repositories rather
/// than the full apply/booking application-service pipeline (already covered by this module's own
/// dedicated test suites) - this suite's own focus is exclusively the READ query's counting/summing
/// correctness against real rows.
/// </summary>
[Collection(CareerApiTestCollectionDefinition.Name)]
public sealed class CareerReportingQueryAdapterTests(CareerServiceFixture fixture)
{
    [Fact]
    public async Task Snapshot_counts_Internships_Drives_CareerApplications_and_sums_InterviewSlot_bookings()
    {
        using var scope = fixture.Services.CreateScope();
        var internshipService = scope.ServiceProvider.GetRequiredService<InternshipService>();
        var driveService = scope.ServiceProvider.GetRequiredService<CampusRecruitmentDriveService>();
        var interviewSlotService = scope.ServiceProvider.GetRequiredService<InterviewSlotService>();
        var interviewSlots = scope.ServiceProvider.GetRequiredService<IInterviewSlotRepository>();
        var careerApplications = scope.ServiceProvider.GetRequiredService<ICareerApplicationRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var query = scope.ServiceProvider.GetRequiredService<ICareerReportingQuery>();

        // Deltas, not absolute totals - this fixture's Postgres instance is shared across every test
        // in this collection (CareerApiTestCollectionDefinition).
        var before = await query.GetDashboardSnapshotAsync();

        var now = DateTimeOffset.UtcNow;
        var employerProfileId = Guid.NewGuid();

        // A Published Internship counts toward PublishedInternshipCount (IsBrowsable()); a still-Draft
        // one counts toward TotalInternshipCount but must NOT be counted as published.
        var publishedInternship = await internshipService.CreateAsync(new CreateInternshipRequest(
            employerProfileId, "Backend Intern", "d", "Dhaka", null, now.AddDays(30), new EligibilityCriteriaDto([], null, null)));
        Assert.True(publishedInternship.IsSuccess);
        var published = await internshipService.PublishAsync(publishedInternship.Value.Id, publishedInternship.Value.Version);
        Assert.True(published.IsSuccess);

        var draftInternship = await internshipService.CreateAsync(new CreateInternshipRequest(
            employerProfileId, "Draft Intern", "d", "Dhaka", null, now.AddDays(30), new EligibilityCriteriaDto([], null, null)));
        Assert.True(draftInternship.IsSuccess);

        // A CampusRecruitmentDrive - counts toward TotalCampusRecruitmentDriveCount regardless of status.
        var drive = await driveService.CreateAsync(new CreateDriveRequest(
            employerProfileId, "Autumn Recruitment Drive", Guid.NewGuid(), now.AddDays(10), now.AddDays(1), now.AddDays(5)));
        Assert.True(drive.IsSuccess);

        // Two InterviewSlots on that Drive - one claimed (BookedCount 1), one untouched (BookedCount 0).
        var slots = await interviewSlotService.DefineSlotsAsync(drive.Value.Id, new DefineInterviewSlotsRequest([
            new DefineInterviewSlotRequest(now.AddDays(10), now.AddDays(10).AddMinutes(30), 1),
            new DefineInterviewSlotRequest(now.AddDays(10).AddHours(1), now.AddDays(10).AddHours(1).AddMinutes(30), 1),
        ]));
        Assert.True(slots.IsSuccess);
        var claimed = await interviewSlots.TryClaimSlotAsync(new InterviewSlotId(slots.Value[0].Id));
        Assert.True(claimed);

        // Two CareerApplications against the published Internship - written directly via the
        // repository (see this class's own remarks) since the full guarded-insert apply flow is
        // already covered elsewhere; this suite only verifies the READ side counts them.
        var resumeSnapshot = new ResumeSnapshot(Guid.NewGuid(), Guid.NewGuid(), "resume.pdf", now);
        careerApplications.Add(CareerApplication.SubmitForInternship(Guid.NewGuid(), new InternshipId(publishedInternship.Value.Id), null, null, resumeSnapshot, now));
        careerApplications.Add(CareerApplication.SubmitForInternship(Guid.NewGuid(), new InternshipId(publishedInternship.Value.Id), null, null, resumeSnapshot, now));
        await unitOfWork.SaveChangesAsync();

        var after = await query.GetDashboardSnapshotAsync();

        Assert.Equal(2, after.TotalInternshipCount - before.TotalInternshipCount);
        Assert.Equal(1, after.PublishedInternshipCount - before.PublishedInternshipCount);
        Assert.Equal(1, after.TotalCampusRecruitmentDriveCount - before.TotalCampusRecruitmentDriveCount);
        Assert.Equal(2, after.TotalCareerApplicationCount - before.TotalCareerApplicationCount);
        Assert.Equal(1, after.TotalInterviewSlotBookings - before.TotalInterviewSlotBookings);
    }
}
