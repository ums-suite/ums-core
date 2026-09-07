using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Admission.Application.Applications;
using UMS.Modules.Admission.IntegrationTests.Infrastructure;

namespace UMS.Modules.Admission.IntegrationTests.Applications;

/// <summary>design-decisions.md "Idempotency for Application Submission": edge-cases.md's "Duplicate submit click / retried network request" - a genuine concurrent race (Task.WhenAll), not a sequential retry, against the real state-guarded conditional write. Every step below opens its OWN DI scope, mirroring a real distinct HTTP request - EF's owned-collection change tracking for an already-tracked aggregate re-queried within the SAME DbContext is not what production ever does (one scope per request), so this suite never exercises that different, untested shape either.</summary>
[Collection(AdmissionApiTestCollectionDefinition.Name)]
public sealed class ApplicationConcurrencyTests(AdmissionServiceFixture fixture)
{
    private async Task<Guid> SeedSubmittableApplicationAsync()
    {
        var programId = Guid.NewGuid();
        var campaign = await AdmissionTestData.SeedCampaignAsync(fixture.Services, programId);
        var applicantId = await AdmissionTestData.SeedVerifiedApplicantAsync(fixture.Services);

        Guid applicationId;
        using (var scope = fixture.Services.CreateScope())
        {
            var created = await scope.ServiceProvider.GetRequiredService<ApplicationService>().CreateDraftAsync(applicantId, campaign.Id);
            Assert.True(created.IsSuccess);
            applicationId = created.Value.Id;
        }

        using (var scope = fixture.Services.CreateScope())
        {
            var replaced = await scope.ServiceProvider.GetRequiredService<ApplicationService>()
                .ReplaceProgramChoicesAsync(applicationId, [new ProgramChoiceRequest(programId, 1)]);
            Assert.True(replaced.IsSuccess);
        }

        Guid invoiceId;
        using (var scope = fixture.Services.CreateScope())
        {
            var invoice = await scope.ServiceProvider.GetRequiredService<ApplicationService>()
                .InitiateApplicationFeePaymentAsync(applicationId, Guid.NewGuid(), "test-correlation");
            Assert.True(invoice.IsSuccess);
            invoiceId = invoice.Value.InvoiceId;
        }

        using (var scope = fixture.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<UMS.Modules.Admission.Application.Abstractions.IApplicationRepository>();
            var paid = await repository.TryMarkApplicationFeePaidAsync(invoiceId);
            Assert.True(paid);
        }

        return applicationId;
    }

    [Fact]
    public async Task Two_concurrent_submit_calls_result_in_exactly_one_Locked_transition_and_one_replay()
    {
        var applicationId = await SeedSubmittableApplicationAsync();

        async Task<UMS.Shared.ErrorHandling.Results.Result<ApplicationDto>> SubmitAsync()
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ApplicationService>();
            return await service.SubmitAsync(applicationId, $"test-{Guid.NewGuid():N}");
        }

        var results = await Task.WhenAll(SubmitAsync(), SubmitAsync());

        Assert.All(results, r => Assert.True(r.IsSuccess));
        Assert.All(results, r => Assert.Equal("Locked", r.Value.Status));
        Assert.Equal(results[0].Value.ApplicationNumber, results[1].Value.ApplicationNumber);
        Assert.NotNull(results[0].Value.ApplicationNumber);
    }

    [Fact]
    public async Task A_repeated_submit_call_against_an_already_Locked_Application_is_a_no_op_replay()
    {
        var applicationId = await SeedSubmittableApplicationAsync();

        UMS.Shared.ErrorHandling.Results.Result<ApplicationDto> first;
        using (var scope = fixture.Services.CreateScope())
        {
            first = await scope.ServiceProvider.GetRequiredService<ApplicationService>().SubmitAsync(applicationId, "test-1");
        }

        UMS.Shared.ErrorHandling.Results.Result<ApplicationDto> second;
        using (var scope = fixture.Services.CreateScope())
        {
            second = await scope.ServiceProvider.GetRequiredService<ApplicationService>().SubmitAsync(applicationId, "test-2");
        }

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.ApplicationNumber, second.Value.ApplicationNumber);
    }
}
