using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Career.Application.Abstractions;
using UMS.Modules.Career.Application.Applications;
using UMS.Modules.Career.Application.Employers;
using UMS.Modules.Career.Application.Internships;
using UMS.Modules.Career.Application.ResumeProfiles;
using UMS.Modules.Career.Application.StudentGraduation;
using UMS.Modules.Career.IntegrationTests.Infrastructure;
using UMS.Shared.Student;

namespace UMS.Modules.Career.IntegrationTests.StudentGraduation;

/// <summary>
/// CAR-16: design-decisions.md "Student-Graduation Boundary for In-Flight Career Activity" - consuming
/// either <c>StudentGraduated</c> or <c>StudentStatusChanged</c> triggers ZERO cascade to any existing
/// <c>CareerApplication</c>; the eligibility gate is a SEPARATE, live <c>Student.status = Active</c>
/// check performed fresh at every new submission attempt, never derived from a consumed event. Mirrors
/// Alumni's own <c>StudentGraduatedConsumptionTests</c> shape, adapted to Career's deliberately
/// "do-nothing" consumer.
/// </summary>
[Collection(CareerApiTestCollectionDefinition.Name)]
public sealed class StudentGraduationBoundaryTests(CareerServiceFixture fixture)
{
    [Fact]
    public async Task Consuming_StudentGraduated_does_not_touch_an_existing_CareerApplication()
    {
        var studentUserId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        fixture.StudentStatusChecker.Register(new StudentAcademicStanding(studentId, Guid.NewGuid(), Guid.NewGuid(), "Active", studentUserId));

        using var scope = fixture.Services.CreateScope();
        var applicationId = await SubmitOneCareerApplicationAsync(scope.ServiceProvider, studentUserId, studentId);

        var occurredAt = DateTimeOffset.UtcNow;
        var payload = $$"""{"StudentId":"{{studentId}}","OccurredAt":"{{occurredAt:O}}"}""";
        await fixture.InsertStudentOutboxMessageAsync(Guid.NewGuid(), "UMS.Modules.Student.Domain.Events.StudentGraduated", payload, occurredAt, occurredAt);

        var consumer = scope.ServiceProvider.GetRequiredService<StudentStatusEventConsumerService>();
        var processed = await consumer.ConsumeUnprocessedAsync(10);
        Assert.Equal(1, processed);

        // Acknowledged, not reprocessed.
        var eventSource = scope.ServiceProvider.GetRequiredService<IStudentStatusEventSource>();
        Assert.Empty(await eventSource.GetUnprocessedAsync(10));

        // The existing CareerApplication is untouched - still Submitted, no cascade of any kind.
        var reviewService = scope.ServiceProvider.GetRequiredService<CareerApplicationReviewService>();
        var application = await reviewService.GetByIdAsync(applicationId);
        Assert.True(application.IsSuccess);
        Assert.Equal("Submitted", application.Value.Status);
    }

    [Fact]
    public async Task Consuming_StudentStatusChanged_does_not_touch_an_existing_CareerApplication()
    {
        var studentUserId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        fixture.StudentStatusChecker.Register(new StudentAcademicStanding(studentId, Guid.NewGuid(), Guid.NewGuid(), "Active", studentUserId));

        using var scope = fixture.Services.CreateScope();
        var applicationId = await SubmitOneCareerApplicationAsync(scope.ServiceProvider, studentUserId, studentId);

        var occurredAt = DateTimeOffset.UtcNow;
        var payload = $$"""{"StudentId":"{{studentId}}","OccurredAt":"{{occurredAt:O}}"}""";
        await fixture.InsertStudentOutboxMessageAsync(Guid.NewGuid(), "UMS.Modules.Student.Domain.Events.StudentStatusChanged", payload, occurredAt, occurredAt);

        var consumer = scope.ServiceProvider.GetRequiredService<StudentStatusEventConsumerService>();
        Assert.Equal(1, await consumer.ConsumeUnprocessedAsync(10));

        var reviewService = scope.ServiceProvider.GetRequiredService<CareerApplicationReviewService>();
        var application = await reviewService.GetByIdAsync(applicationId);
        Assert.True(application.IsSuccess);
        Assert.Equal("Submitted", application.Value.Status);
    }

    /// <summary>design-decisions.md: the REAL eligibility gate is a fresh, live read at every new submission - never derived from a consumed event (whether or not it was ever consumed/acknowledged).</summary>
    [Fact]
    public async Task A_new_submission_is_rejected_the_moment_the_live_Student_status_is_no_longer_Active_even_without_any_consumed_event()
    {
        var studentUserId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        fixture.StudentStatusChecker.Register(new StudentAcademicStanding(studentId, Guid.NewGuid(), Guid.NewGuid(), "Active", studentUserId));

        using var scope = fixture.Services.CreateScope();
        var (internshipId, resumeProfileId) = await PublishOneOpenInternshipWithAResumeAsync(scope.ServiceProvider, studentId);

        // Flip the live standing to Graduated - no StudentGraduated outbox event is ever inserted or consumed.
        fixture.StudentStatusChecker.Register(new StudentAcademicStanding(studentId, Guid.NewGuid(), Guid.NewGuid(), "Graduated", studentUserId));

        var applicationService = scope.ServiceProvider.GetRequiredService<InternshipApplicationService>();
        var result = await applicationService.ApplyAsync(studentUserId, internshipId, new ApplyToInternshipRequest(resumeProfileId, 3.8m, 4));

        Assert.True(result.IsFailure);
        Assert.Equal("careerapplication.student_not_active", result.Error!.Code);
    }

    private static async Task<Guid> SubmitOneCareerApplicationAsync(IServiceProvider services, Guid studentUserId, Guid studentId)
    {
        var (internshipId, resumeProfileId) = await PublishOneOpenInternshipWithAResumeAsync(services, studentId);

        var applicationService = services.GetRequiredService<InternshipApplicationService>();
        var result = await applicationService.ApplyAsync(studentUserId, internshipId, new ApplyToInternshipRequest(resumeProfileId, 3.8m, 4));
        Assert.True(result.IsSuccess);
        return result.Value.Id;
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
