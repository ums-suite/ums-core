using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Career.Application.Applications;
using UMS.Modules.Career.Application.Employers;
using UMS.Modules.Career.Application.Internships;
using UMS.Modules.Career.Application.ResumeProfiles;
using UMS.Modules.Career.IntegrationTests.Infrastructure;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Student;

namespace UMS.Modules.Career.IntegrationTests.Applications;

/// <summary>
/// CAR-6: design-decisions.md "Internship/Drive Application-Window Write-Time Enforcement" - a
/// single guarded <c>INSERT ... SELECT ... WHERE</c> re-checking the window AND the
/// at-most-one-non-terminal-application-per-(Student, Internship) invariant in the SAME statement as
/// the insert itself, mirroring Alumni's own JobPosting-expiry-vs-apply race mechanism. A genuine
/// concurrent race - real <see cref="Task.WhenAll"/>, each branch its own DI scope - never a
/// sequential retry.
/// </summary>
[Collection(CareerApiTestCollectionDefinition.Name)]
public sealed class ApplicationWindowGuardTests(CareerServiceFixture fixture)
{
    [Fact]
    public async Task Two_concurrent_submissions_from_the_same_Student_against_the_same_Internship_produce_exactly_one_CareerApplication()
    {
        var studentUserId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        fixture.StudentStatusChecker.Register(new StudentAcademicStanding(studentId, Guid.NewGuid(), Guid.NewGuid(), "Active", studentUserId));

        Guid internshipId;
        Guid resumeProfileId;
        using (var setupScope = fixture.Services.CreateScope())
        {
            (internshipId, resumeProfileId) = await PublishOneOpenInternshipWithAResumeAsync(setupScope.ServiceProvider, studentId);
        }

        async Task<Result<CareerApplicationDto>> ApplyAsync()
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<InternshipApplicationService>();
            return await service.ApplyAsync(studentUserId, internshipId, new ApplyToInternshipRequest(resumeProfileId, 3.5m, 3));
        }

        var results = await Task.WhenAll(ApplyAsync(), ApplyAsync());

        Assert.Single(results, r => r.IsSuccess);
        Assert.Single(results, r => r.IsFailure && r.Error!.Code == "internship.not_accepting_applications");

        using var verifyScope = fixture.Services.CreateScope();
        var reviewService = verifyScope.ServiceProvider.GetRequiredService<CareerApplicationReviewService>();
        var applications = await reviewService.ListByStudentAsync(studentId);
        Assert.Single(applications);
    }

    [Fact]
    public async Task A_submission_is_rejected_once_the_Internship_has_closed_applications()
    {
        var studentUserId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        fixture.StudentStatusChecker.Register(new StudentAcademicStanding(studentId, Guid.NewGuid(), Guid.NewGuid(), "Active", studentUserId));

        using var scope = fixture.Services.CreateScope();
        var internshipService = scope.ServiceProvider.GetRequiredService<InternshipService>();
        var (internshipId, resumeProfileId) = await PublishOneOpenInternshipWithAResumeAsync(scope.ServiceProvider, studentId);

        var internship = await internshipService.GetByIdAsync(internshipId);
        var closed = await internshipService.CloseApplicationsAsync(internshipId, internship.Value.Version);
        Assert.True(closed.IsSuccess);

        var applicationService = scope.ServiceProvider.GetRequiredService<InternshipApplicationService>();
        var result = await applicationService.ApplyAsync(studentUserId, internshipId, new ApplyToInternshipRequest(resumeProfileId, 3.5m, 3));

        Assert.True(result.IsFailure);
        Assert.Equal("internship.not_accepting_applications", result.Error!.Code);
    }

    private static async Task<(Guid InternshipId, Guid ResumeProfileId)> PublishOneOpenInternshipWithAResumeAsync(IServiceProvider services, Guid studentId)
    {
        var employerService = services.GetRequiredService<EmployerProfileService>();
        var internshipService = services.GetRequiredService<InternshipService>();
        var resumeService = services.GetRequiredService<ResumeProfileService>();

        var employer = await employerService.CreateAsync(new CreateEmployerProfileRequest("Acme Corp", "Software", null, "Jane Doe", "jane@acme.example", null, null));
        Assert.True(employer.IsSuccess);

        var now = DateTimeOffset.UtcNow;
        var internship = await internshipService.CreateAsync(new CreateInternshipRequest(employer.Value.Id, "Backend Intern", "desc", "Remote", null, now.AddDays(30), new EligibilityCriteriaDto([], null, null)));
        Assert.True(internship.IsSuccess);

        var published = await internshipService.PublishAsync(internship.Value.Id, internship.Value.Version);
        Assert.True(published.IsSuccess);
        var opened = await internshipService.OpenApplicationsAsync(internship.Value.Id, published.Value.Version);
        Assert.True(opened.IsSuccess);

        var uploadSlot = await resumeService.RequestUploadAsync(studentId, new RequestResumeUploadRequest("application/pdf"));
        Assert.True(uploadSlot.IsSuccess);
        var resumeProfile = await resumeService.CreateAsync(studentId, new CreateResumeProfileRequest("General", uploadSlot.Value.ArtifactId, true));
        Assert.True(resumeProfile.IsSuccess);

        return (opened.Value.Id, resumeProfile.Value.Id);
    }
}
