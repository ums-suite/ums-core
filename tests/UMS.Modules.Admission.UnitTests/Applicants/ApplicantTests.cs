using UMS.Modules.Admission.Domain.Applicants;

namespace UMS.Modules.Admission.UnitTests.Applicants;

/// <summary>ADM-2/ADM-3: requirement-spec.md §2 Applicant Registration &amp; Verification.</summary>
public sealed class ApplicantTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Applicant Register() =>
        Applicant.Register(Guid.NewGuid(), "Rahim", "Uddin", "rahim@example.com", "+8801700000000", new DateOnly(2005, 1, 1), Now).Value;

    [Fact]
    public void A_freshly_registered_applicant_is_not_verified()
    {
        var applicant = Register();

        Assert.False(applicant.IsVerified);
    }

    [Fact]
    public void Verifying_the_correct_otp_marks_the_channel_verified()
    {
        var applicant = Register();
        applicant.IssueVerificationOtp(OtpChannel.Email, "hash-of-123456", Now.AddMinutes(10));

        var result = applicant.VerifyOtp(OtpChannel.Email, "hash-of-123456", Now.AddMinutes(1));

        Assert.True(result.IsSuccess);
        Assert.True(applicant.IsEmailVerified);
        Assert.True(applicant.IsVerified);
    }

    [Fact]
    public void An_incorrect_otp_is_rejected_without_verifying()
    {
        var applicant = Register();
        applicant.IssueVerificationOtp(OtpChannel.Mobile, "hash-of-123456", Now.AddMinutes(10));

        var result = applicant.VerifyOtp(OtpChannel.Mobile, "hash-of-wrong", Now.AddMinutes(1));

        Assert.True(result.IsFailure);
        Assert.Equal("applicant.otp_incorrect", result.Error!.Code);
        Assert.False(applicant.IsMobileVerified);
    }

    [Fact]
    public void An_expired_otp_is_rejected()
    {
        var applicant = Register();
        applicant.IssueVerificationOtp(OtpChannel.Email, "hash-of-123456", Now.AddMinutes(10));

        var result = applicant.VerifyOtp(OtpChannel.Email, "hash-of-123456", Now.AddMinutes(11));

        Assert.True(result.IsFailure);
        Assert.Equal("applicant.otp_expired", result.Error!.Code);
    }

    [Fact]
    public void Five_incorrect_attempts_exhausts_the_pending_otp()
    {
        var applicant = Register();
        applicant.IssueVerificationOtp(OtpChannel.Email, "hash-of-123456", Now.AddMinutes(10));

        for (var i = 0; i < 5; i++)
        {
            applicant.VerifyOtp(OtpChannel.Email, "wrong", Now);
        }

        var result = applicant.VerifyOtp(OtpChannel.Email, "hash-of-123456", Now);

        Assert.True(result.IsFailure);
        Assert.Equal("applicant.otp_attempts_exceeded", result.Error!.Code);
    }

    [Fact]
    public void Requesting_a_fresh_otp_replaces_any_prior_pending_code()
    {
        var applicant = Register();
        applicant.IssueVerificationOtp(OtpChannel.Email, "hash-of-111111", Now);
        applicant.IssueVerificationOtp(OtpChannel.Email, "hash-of-222222", Now.AddMinutes(10));

        var stale = applicant.VerifyOtp(OtpChannel.Email, "hash-of-111111", Now.AddMinutes(1));

        Assert.True(stale.IsFailure);
        Assert.Equal("applicant.otp_incorrect", stale.Error!.Code);
    }
}
