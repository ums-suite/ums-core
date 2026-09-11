using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Career.Application.Applications;
using UMS.Modules.Career.Application.Drives;
using UMS.Modules.Career.Application.Employers;
using UMS.Modules.Career.Application.ResumeProfiles;
using UMS.Modules.Career.IntegrationTests.Infrastructure;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Student;

namespace UMS.Modules.Career.IntegrationTests.Drives;

/// <summary>
/// CAR-12: design-decisions.md "Interview-Slot Booking Concurrency Control"; edge-cases.md "Two
/// shortlisted Students racing to book the last InterviewSlot" - a genuine concurrent race (real
/// <see cref="Task.WhenAll"/>, each branch its own DI scope) against the real atomic conditional
/// <c>UPDATE ... WHERE booked_count &lt; capacity</c>, never a sequential retry. Mirrors Alumni's own
/// <c>MentorCapacityConcurrencyTests</c> shape - this module's explicitly named highest-concurrency
/// write path (requirement-spec.md §5 Concurrency NFR).
/// </summary>
[Collection(CareerApiTestCollectionDefinition.Name)]
public sealed class InterviewSlotBookingConcurrencyTests(CareerServiceFixture fixture)
{
    [Fact]
    public async Task Two_concurrent_bookings_against_a_slot_with_exactly_one_seat_remaining_exactly_one_wins()
    {
        var (driveId, slotId, applicationAId, applicationBId, studentAId, studentBId) = await SeedTwoShortlistedApplicationsAgainstAOneSeatSlotAsync();

        async Task<Result<CareerApplicationDto>> BookAsync(Guid careerApplicationId, Guid callerStudentId)
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<InterviewSlotService>();
            return await service.BookAsync(driveId, slotId, careerApplicationId, callerStudentId);
        }

        var results = await Task.WhenAll(
            BookAsync(applicationAId, studentAId),
            BookAsync(applicationBId, studentBId));

        Assert.Single(results, r => r.IsSuccess);
        Assert.Single(results, r => r.IsFailure && r.Error!.Code == "interviewslot.no_capacity");

        using var verifyScope = fixture.Services.CreateScope();
        var slots = verifyScope.ServiceProvider.GetRequiredService<InterviewSlotService>();
        var slot = (await slots.ListByDriveAsync(driveId)).Single(s => s.Id == slotId);
        Assert.Equal(1, slot.BookedCount);
        Assert.False(slot.BookedCount > slot.Capacity);
    }

    [Fact]
    public async Task Cancelling_a_booking_releases_capacity_for_a_subsequent_booking()
    {
        var (driveId, slotId, applicationAId, applicationBId, studentAId, studentBId) = await SeedTwoShortlistedApplicationsAgainstAOneSeatSlotAsync();

        using var scope = fixture.Services.CreateScope();
        var slotService = scope.ServiceProvider.GetRequiredService<InterviewSlotService>();

        var first = await slotService.BookAsync(driveId, slotId, applicationAId, studentAId);
        Assert.True(first.IsSuccess);

        var secondBeforeCancel = await slotService.BookAsync(driveId, slotId, applicationBId, studentBId);
        Assert.True(secondBeforeCancel.IsFailure);

        var cancelled = await slotService.CancelBookingAsync(driveId, slotId, applicationAId, studentAId);
        Assert.True(cancelled.IsSuccess);

        var secondAfterCancel = await slotService.BookAsync(driveId, slotId, applicationBId, studentBId);
        Assert.True(secondAfterCancel.IsSuccess);
    }

    private async Task<(Guid DriveId, Guid SlotId, Guid ApplicationAId, Guid ApplicationBId, Guid StudentAId, Guid StudentBId)> SeedTwoShortlistedApplicationsAgainstAOneSeatSlotAsync()
    {
        var studentAUserId = Guid.NewGuid();
        var studentBUserId = Guid.NewGuid();
        var studentAId = Guid.NewGuid();
        var studentBId = Guid.NewGuid();

        fixture.StudentStatusChecker.Register(new StudentAcademicStanding(studentAId, Guid.NewGuid(), Guid.NewGuid(), "Active", studentAUserId));
        fixture.StudentStatusChecker.Register(new StudentAcademicStanding(studentBId, Guid.NewGuid(), Guid.NewGuid(), "Active", studentBUserId));

        using var scope = fixture.Services.CreateScope();
        var employerService = scope.ServiceProvider.GetRequiredService<EmployerProfileService>();
        var driveService = scope.ServiceProvider.GetRequiredService<CampusRecruitmentDriveService>();
        var slotService = scope.ServiceProvider.GetRequiredService<InterviewSlotService>();
        var resumeService = scope.ServiceProvider.GetRequiredService<ResumeProfileService>();
        var driveApplicationService = scope.ServiceProvider.GetRequiredService<DriveApplicationService>();
        var reviewService = scope.ServiceProvider.GetRequiredService<CareerApplicationReviewService>();

        var employer = await employerService.CreateAsync(new CreateEmployerProfileRequest("Acme Corp", "Software", null, "Jane Doe", "jane@acme.example", null, null));
        Assert.True(employer.IsSuccess);

        var now = DateTimeOffset.UtcNow;
        var driveResult = await driveService.CreateAsync(new CreateDriveRequest(employer.Value.Id, "Spring Drive", Guid.NewGuid(), now.AddDays(10), now.AddMinutes(-5), now.AddDays(5)));
        Assert.True(driveResult.IsSuccess);
        var drive = driveResult.Value;

        var scheduled = await driveService.ScheduleAsync(drive.Id, drive.Version);
        Assert.True(scheduled.IsSuccess);
        var openResult = await driveService.OpenRegistrationAsync(drive.Id, scheduled.Value.Version);
        Assert.True(openResult.IsSuccess);

        var resumeAId = await CreateResumeProfileAsync(resumeService, studentAId);
        var resumeBId = await CreateResumeProfileAsync(resumeService, studentBId);

        var applicationA = await driveApplicationService.RegisterAsync(studentAUserId, drive.Id, new RegisterForDriveRequest(resumeAId, 3.5m, 3));
        var applicationB = await driveApplicationService.RegisterAsync(studentBUserId, drive.Id, new RegisterForDriveRequest(resumeBId, 3.5m, 3));
        Assert.True(applicationA.IsSuccess);
        Assert.True(applicationB.IsSuccess);

        var shortlisted = await reviewService.ShortlistAsync([applicationA.Value.Id, applicationB.Value.Id]);
        Assert.Equal(2, shortlisted.Count);

        var slotsResult = await slotService.DefineSlotsAsync(drive.Id, new DefineInterviewSlotsRequest([new DefineInterviewSlotRequest(now.AddDays(9), now.AddDays(9).AddMinutes(30), 1)]));
        Assert.True(slotsResult.IsSuccess);
        var slotId = slotsResult.Value[0].Id;

        return (drive.Id, slotId, applicationA.Value.Id, applicationB.Value.Id, studentAId, studentBId);
    }

    private static async Task<Guid> CreateResumeProfileAsync(ResumeProfileService resumeService, Guid studentId)
    {
        var slot = await resumeService.RequestUploadAsync(studentId, new RequestResumeUploadRequest("application/pdf"));
        Assert.True(slot.IsSuccess);

        var created = await resumeService.CreateAsync(studentId, new CreateResumeProfileRequest("General", slot.Value.ArtifactId, true));
        Assert.True(created.IsSuccess);
        return created.Value.Id;
    }
}
