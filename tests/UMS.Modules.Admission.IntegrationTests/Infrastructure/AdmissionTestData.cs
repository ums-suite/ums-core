using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Admission.Application.Abstractions;
using UMS.Modules.Admission.Application.Campaigns;
using UMS.Modules.Admission.Domain.Applicants;

namespace UMS.Modules.Admission.IntegrationTests.Infrastructure;

/// <summary>Seeds a real, submittable Application chain through the actual Application services (never raw SQL), mirroring Finance's own <c>FinanceTestData</c> exactly.</summary>
internal static class AdmissionTestData
{
    public static async Task<CampaignDto> SeedCampaignAsync(IServiceProvider services, Guid programId)
    {
        using var scope = services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<CampaignService>();
        var result = await service.CreateAsync(new CreateCampaignRequest(
            $"Campaign-{Guid.NewGuid():N}",
            [programId],
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 3, 1),
            "ApplicationFee",
            "ConfirmationFee")).ConfigureAwait(false);
        return result.Value;
    }

    /// <summary>Bypasses ApplicantService's own OTP-hashing (Application-layer-internal) since the domain aggregate itself is hash-agnostic - it only compares whatever string it was given at issue-time against verify-time, so any fixed string round-trips identically for test purposes.</summary>
    public static async Task<Guid> SeedVerifiedApplicantAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IApplicantRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var applicant = Applicant.Register(Guid.NewGuid(), "Test", "Applicant", $"applicant-{Guid.NewGuid():N}@example.com", "+8801700000000", new DateOnly(2005, 1, 1), clock.UtcNow).Value;
        applicant.IssueVerificationOtp(OtpChannel.Email, "fixed-test-hash", clock.UtcNow.AddMinutes(10));
        applicant.VerifyOtp(OtpChannel.Email, "fixed-test-hash", clock.UtcNow);

        repository.Add(applicant);
        await unitOfWork.SaveChangesAsync().ConfigureAwait(false);
        return applicant.Id.Value;
    }
}
