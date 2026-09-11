using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Career.Application.Applications;
using UMS.Modules.Career.Application.Employers;
using UMS.Modules.Career.Application.Internships;
using UMS.Modules.Career.Application.ResumeProfiles;
using UMS.Modules.Career.IntegrationTests.Infrastructure;
using UMS.Shared.Student;

namespace UMS.Modules.Career.IntegrationTests.Applications;

/// <summary>
/// CAR-9: design-decisions.md "Internship/Drive Withdrawal Cascade to CareerApplication" - an
/// in-process domain-event handler (never one giant transaction, never an async worker), each
/// affected <c>CareerApplication</c> transitioned via <c>CancelDueToPostingWithdrawal()</c> in its OWN
/// transaction, with a mandatory <c>NotificationRequest</c> per Student. Exercises the real
/// <see cref="InternshipWithdrawalCascadeHandler"/> against a real Postgres, exactly as the Api layer
/// invokes it - the Internship's own withdrawal commits first, the cascade runs after.
/// </summary>
[Collection(CareerApiTestCollectionDefinition.Name)]
public sealed class WithdrawalCascadeTests(CareerServiceFixture fixture)
{
    [Fact]
    public async Task Withdrawing_an_Internship_cancels_every_non_terminal_CareerApplication_and_notifies_each_Student()
    {
        var studentAUserId = Guid.NewGuid();
        var studentBUserId = Guid.NewGuid();
        var studentAId = Guid.NewGuid();
        var studentBId = Guid.NewGuid();
        fixture.StudentStatusChecker.Register(new StudentAcademicStanding(studentAId, Guid.NewGuid(), Guid.NewGuid(), "Active", studentAUserId));
        fixture.StudentStatusChecker.Register(new StudentAcademicStanding(studentBId, Guid.NewGuid(), Guid.NewGuid(), "Active", studentBUserId));

        using var scope = fixture.Services.CreateScope();
        var employerService = scope.ServiceProvider.GetRequiredService<EmployerProfileService>();
        var internshipService = scope.ServiceProvider.GetRequiredService<InternshipService>();
        var resumeService = scope.ServiceProvider.GetRequiredService<ResumeProfileService>();
        var applicationService = scope.ServiceProvider.GetRequiredService<InternshipApplicationService>();
        var reviewService = scope.ServiceProvider.GetRequiredService<CareerApplicationReviewService>();
        var cascadeHandler = scope.ServiceProvider.GetRequiredService<InternshipWithdrawalCascadeHandler>();

        var employer = await employerService.CreateAsync(new CreateEmployerProfileRequest("Acme Corp", "Software", null, "Jane Doe", "jane@acme.example", null, null));
        Assert.True(employer.IsSuccess);

        var now = DateTimeOffset.UtcNow;
        var internship = await internshipService.CreateAsync(new CreateInternshipRequest(employer.Value.Id, "Backend Intern", "desc", "Remote", null, now.AddDays(30), new EligibilityCriteriaDto([], null, null)));
        var published = await internshipService.PublishAsync(internship.Value.Id, internship.Value.Version);
        var opened = await internshipService.OpenApplicationsAsync(internship.Value.Id, published.Value.Version);
        Assert.True(opened.IsSuccess);

        var resumeAId = await CreateResumeProfileAsync(resumeService, studentAId);
        var resumeBId = await CreateResumeProfileAsync(resumeService, studentBId);

        var applicationA = await applicationService.ApplyAsync(studentAUserId, internship.Value.Id, new ApplyToInternshipRequest(resumeAId, 3.8m, 4));
        var applicationB = await applicationService.ApplyAsync(studentBUserId, internship.Value.Id, new ApplyToInternshipRequest(resumeBId, 3.6m, 3));
        Assert.True(applicationA.IsSuccess);
        Assert.True(applicationB.IsSuccess);

        // The posting's own withdrawal commits first (mirrors the Api layer's own two-step call).
        var withdrawn = await internshipService.WithdrawAsync(internship.Value.Id, new WithdrawInternshipRequest("Employer pulled the offer", opened.Value.Version));
        Assert.True(withdrawn.IsSuccess);
        Assert.Equal("Withdrawn", withdrawn.Value.Status);

        var cancelledCount = await cascadeHandler.HandleAsync(internship.Value.Id, "test-correlation-id");
        Assert.Equal(2, cancelledCount);

        var refreshedA = await reviewService.GetByIdAsync(applicationA.Value.Id);
        var refreshedB = await reviewService.GetByIdAsync(applicationB.Value.Id);
        Assert.Equal("Cancelled", refreshedA.Value.Status);
        Assert.Equal("Cancelled", refreshedB.Value.Status);

        // Mandatory NotificationRequest per Student (requirement-spec.md §2.6).
        var notifications = fixture.NotificationRequestIntake.Requests.Where(r => r.EventType == "CareerApplicationCancelled").ToList();
        Assert.Equal(2, notifications.Count);
        Assert.Contains(notifications, n => n.RecipientId == studentAUserId);
        Assert.Contains(notifications, n => n.RecipientId == studentBUserId);
    }

    private static async Task<Guid> CreateResumeProfileAsync(ResumeProfileService resumeService, Guid studentId)
    {
        var slot = await resumeService.RequestUploadAsync(studentId, new RequestResumeUploadRequest("application/pdf"));
        var created = await resumeService.CreateAsync(studentId, new CreateResumeProfileRequest("General", slot.Value.ArtifactId, true));
        return created.Value.Id;
    }
}
