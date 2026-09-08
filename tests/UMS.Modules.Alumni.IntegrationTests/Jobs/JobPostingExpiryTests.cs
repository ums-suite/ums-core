using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using UMS.Modules.Alumni.Application.Jobs;
using UMS.Modules.Alumni.IntegrationTests.Infrastructure;

namespace UMS.Modules.Alumni.IntegrationTests.Jobs;

/// <summary>
/// ALM-6/ALM-7: requirement-spec.md §4 "A JobPosting past its expires_at cannot be applied to -
/// enforced at write time"; edge-cases.md "JobPosting expiry sweep racing a concurrent application
/// submission" - the guarded <c>INSERT ... SELECT ... WHERE EXISTS (status = Published AND
/// expires_at &gt; now())</c> re-validates against the row's CURRENT committed state at the instant of
/// the write, not a value read earlier - so it rejects an application even against a row still
/// carrying <c>status = Published</c> in the DB (the sweep just hasn't run yet) the moment its own
/// <c>expires_at</c> has genuinely passed, exactly the "recheck inside the write" guarantee the spec
/// requires.
///
/// <para>
/// Each test forces <c>expires_at</c> into the past via a PLAIN Npgsql connection (never through the
/// fixture's own tracked <c>AlumniDbContext</c>) and re-reads through a FRESH DI scope afterwards -
/// ums-core-gotchas' own "stale-tracked-entity-after-raw-SQL" trap: updating the row through the SAME
/// already-tracked <c>DbContext</c> that created it leaves the in-memory entity's <c>ExpiresAt</c>
/// stale, so a subsequent query on that same context's identity map silently returns the
/// pre-update instance instead of reflecting the raw write (confirmed against this exact test suite
/// before this fix - the sweep found zero due postings until scopes were separated).
/// </para>
/// </summary>
[Collection(AlumniApiTestCollectionDefinition.Name)]
public sealed class JobPostingExpiryTests(AlumniServiceFixture fixture)
{
    private async Task ForceExpiryIntoThePastAsync(Guid jobPostingId)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("UPDATE alumni.job_postings SET expires_at = now() - interval '1 day' WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", jobPostingId);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Applying_to_a_Published_posting_whose_expires_at_has_already_passed_is_rejected()
    {
        Guid jobPostingId;
        using (var postScope = fixture.Services.CreateScope())
        {
            var jobPostingService = postScope.ServiceProvider.GetRequiredService<JobPostingService>();
            var posted = await jobPostingService.PostAsync(
                Guid.NewGuid(), posterIsAlumnus: true, Guid.NewGuid(),
                new PostJobRequest("SWE", "Acme", "desc", "Dhaka", "email", DateTimeOffset.UtcNow.AddDays(30)));
            Assert.True(posted.IsSuccess);
            jobPostingId = posted.Value.Id;
        }

        await ForceExpiryIntoThePastAsync(jobPostingId);

        using var applyScope = fixture.Services.CreateScope();
        var applicationService = applyScope.ServiceProvider.GetRequiredService<JobApplicationService>();
        var rejected = await applicationService.ApplyAsync(jobPostingId, Guid.NewGuid(), applicantIsAlumnus: false, new ApplyToJobRequest(null, null));

        Assert.True(rejected.IsFailure);
        Assert.Equal("jobposting.not_accepting_applications", rejected.Error!.Code);
    }

    [Fact]
    public async Task The_sweep_transitions_a_genuinely_overdue_Published_posting_to_Expired_and_stops_accepting_applications()
    {
        Guid jobPostingId;
        using (var postScope = fixture.Services.CreateScope())
        {
            var jobPostingService = postScope.ServiceProvider.GetRequiredService<JobPostingService>();
            var posted = await jobPostingService.PostAsync(
                Guid.NewGuid(), posterIsAlumnus: true, Guid.NewGuid(),
                new PostJobRequest("SWE", "Acme", "desc", "Dhaka", "email", DateTimeOffset.UtcNow.AddDays(30)));
            Assert.True(posted.IsSuccess);
            jobPostingId = posted.Value.Id;
        }

        await ForceExpiryIntoThePastAsync(jobPostingId);

        using (var sweepScope = fixture.Services.CreateScope())
        {
            var expiryService = sweepScope.ServiceProvider.GetRequiredService<JobPostingExpiryService>();
            var expiredCount = await expiryService.ExpireDueAsync(100);
            Assert.Equal(1, expiredCount);
        }

        using var verifyScope = fixture.Services.CreateScope();
        var jobPostingService2 = verifyScope.ServiceProvider.GetRequiredService<JobPostingService>();
        var applicationService = verifyScope.ServiceProvider.GetRequiredService<JobApplicationService>();

        var reloaded = await jobPostingService2.GetByIdAsync(jobPostingId);
        Assert.True(reloaded.IsSuccess);
        Assert.Equal("Expired", reloaded.Value.Status);

        var rejected = await applicationService.ApplyAsync(jobPostingId, Guid.NewGuid(), applicantIsAlumnus: false, new ApplyToJobRequest(null, null));
        Assert.True(rejected.IsFailure);
    }

    [Fact]
    public async Task A_Removed_posting_no_longer_accepts_applications()
    {
        using var scope = fixture.Services.CreateScope();
        var jobPostingService = scope.ServiceProvider.GetRequiredService<JobPostingService>();
        var applicationService = scope.ServiceProvider.GetRequiredService<JobApplicationService>();

        var posted = await jobPostingService.PostAsync(
            Guid.NewGuid(), posterIsAlumnus: true, Guid.NewGuid(),
            new PostJobRequest("SWE", "Acme", "desc", "Dhaka", "email", DateTimeOffset.UtcNow.AddDays(30)));
        Assert.True(posted.IsSuccess);

        var removed = await jobPostingService.RemoveAsync(
            posted.Value.Id,
            new RemoveJobPostingRequest("reported as spam", posted.Value.Version),
            new UMS.Modules.Alumni.Application.Common.AuditContext(Guid.NewGuid(), null, Guid.NewGuid().ToString()));
        Assert.True(removed.IsSuccess);

        var rejected = await applicationService.ApplyAsync(posted.Value.Id, Guid.NewGuid(), applicantIsAlumnus: false, new ApplyToJobRequest(null, null));
        Assert.True(rejected.IsFailure);
        Assert.Equal("jobposting.not_accepting_applications", rejected.Error!.Code);
    }
}
