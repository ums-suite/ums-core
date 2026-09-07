using UMS.Modules.Admission.Domain.Common;
using UMS.Modules.Admission.Domain.Events;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Admission.Domain.Applicants;

public enum OtpChannel
{
    Email,
    Mobile,
}

/// <summary>
/// ADM-2/ADM-3: a prospective student's identity/profile prior to any admission decision
/// (docs/ddd/ubiquitous-language.md). One <see cref="Applicant"/> may apply to more than one
/// <see cref="Campaigns.AdmissionCampaign"/> over time (requirement-spec.md §2) - <see cref="Applicant"/>
/// itself carries no reference to any specific <see cref="Applications.Application"/>.
///
/// <para>
/// <b>Identity linkage.</b> requirement-spec.md §6 gates every later endpoint "Applicant-owned" -
/// an Applicant must be able to authenticate, so registration (ADM-2) provisions a real, login-
/// capable Identity <c>User</c> via <c>UMS.Shared.Identity.IUserProvisioner</c> (the same contract
/// Student's own onboarding uses), and <see cref="IdentityUserId"/> is that provisioned User's id -
/// the join key every later Applicant-owned endpoint authorizes against (the caller's JWT `sub`).
/// </para>
///
/// <para>
/// <b>OTP verification mechanism (ADM-3; requirement-spec.md §2: "mobile and/or email verification
/// (OTP) is required before an Application may be created").</b> No shared, reusable OTP-issuance
/// contract exists elsewhere in this codebase yet (Identity's own OTP-shaped mechanisms - MFA TOTP,
/// password-reset tokens - are both scoped to an already-provisioned User's OWN login recovery, not
/// a pre-verification gate for a new registrant) - Admission owns a minimal one itself, mirroring
/// Identity's own password-reset-token mechanism exactly: a random code, stored only as a hash with
/// a short TTL, delivered through Notifications' real <c>INotificationRequestIntake</c> (never
/// emailed/SMSed by Admission itself, ADR-0009), and a bounded verification-attempt counter as a
/// coarse anti-brute-force measure (mirrors Identity's own failed-login lockout counter in spirit,
/// scaled down for a low-stakes, short-TTL code).
/// </para>
/// </summary>
public sealed class Applicant : AggregateRoot<ApplicantId>
{
    private const int MaxVerificationAttempts = 5;

    private readonly List<AcademicRecord> _academicHistory = [];

    private Applicant()
    {
    }

    private Applicant(ApplicantId id, Guid identityUserId, string givenName, string familyName, string email, string? mobile, DateOnly dateOfBirth, DateTimeOffset now)
    {
        Id = id;
        IdentityUserId = identityUserId;
        GivenName = givenName;
        FamilyName = familyName;
        Email = email;
        Mobile = mobile;
        DateOfBirth = dateOfBirth;
        CreatedAt = now;
    }

    public Guid IdentityUserId { get; private set; }

    public string GivenName { get; private set; } = string.Empty;

    public string FamilyName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string? Mobile { get; private set; }

    public DateOnly DateOfBirth { get; private set; }

    public bool IsEmailVerified { get; private set; }

    public bool IsMobileVerified { get; private set; }

    public IReadOnlyCollection<AcademicRecord> AcademicHistory => _academicHistory.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>requirement-spec.md §2: at least one channel verified is the submit-time precondition (ADM-4's own gate reads this, not a specific channel) - a real deployment may require both; this build treats either as sufficient, a first-pass call not stated in the BRD.</summary>
    public bool IsVerified => IsEmailVerified || IsMobileVerified;

    public string? PendingOtpHash { get; private set; }

    public OtpChannel? PendingOtpChannel { get; private set; }

    public DateTimeOffset? PendingOtpExpiresAt { get; private set; }

    public int PendingOtpAttempts { get; private set; }

    public static Result<Applicant> Register(Guid identityUserId, string givenName, string familyName, string email, string? mobile, DateOnly dateOfBirth, DateTimeOffset now)
    {
        if (identityUserId == Guid.Empty)
        {
            return Error.Validation("applicant.identity_user_id_required", "An Applicant must be linked to a provisioned Identity User.");
        }

        if (string.IsNullOrWhiteSpace(givenName) || string.IsNullOrWhiteSpace(familyName))
        {
            return Error.Validation("applicant.name_required", "An Applicant's given/family name is required.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return Error.Validation("applicant.email_required", "An Applicant's email is required.");
        }

        var applicant = new Applicant(ApplicantId.New(), identityUserId, givenName.Trim(), familyName.Trim(), email.Trim(), string.IsNullOrWhiteSpace(mobile) ? null : mobile.Trim(), dateOfBirth, now);
        return applicant;
    }

    /// <summary>ADM-3: issues a new OTP for the given channel - replaces any prior pending code (a fresh request always supersedes, mirroring Identity's own MFA-enrollment-secret "replaced on every new call" mechanism), resetting the attempt counter.</summary>
    public void IssueVerificationOtp(OtpChannel channel, string otpHash, DateTimeOffset expiresAt)
    {
        PendingOtpChannel = channel;
        PendingOtpHash = otpHash;
        PendingOtpExpiresAt = expiresAt;
        PendingOtpAttempts = 0;
    }

    /// <summary>ADM-3: verifies a caller-supplied OTP against the stored hash. A wrong code counts against the bounded attempt budget (never lets an unlimited number of guesses hit the same short code); an expired or already-consumed code is rejected outright without counting an attempt.</summary>
    public Result VerifyOtp(OtpChannel channel, string candidateHash, DateTimeOffset now)
    {
        if (PendingOtpHash is null || PendingOtpChannel != channel)
        {
            return Result.Failure(Error.Conflict("applicant.no_pending_otp", "No pending verification code exists for this channel - request one first."));
        }

        if (PendingOtpExpiresAt is null || now > PendingOtpExpiresAt)
        {
            ClearPendingOtp();
            return Result.Failure(Error.Validation("applicant.otp_expired", "The verification code has expired - request a new one."));
        }

        if (PendingOtpAttempts >= MaxVerificationAttempts)
        {
            ClearPendingOtp();
            return Result.Failure(Error.Conflict("applicant.otp_attempts_exceeded", "Too many incorrect attempts - request a new verification code."));
        }

        if (!string.Equals(PendingOtpHash, candidateHash, StringComparison.Ordinal))
        {
            PendingOtpAttempts++;
            return Result.Failure(Error.Validation("applicant.otp_incorrect", "The verification code is incorrect."));
        }

        if (channel == OtpChannel.Email)
        {
            IsEmailVerified = true;
        }
        else
        {
            IsMobileVerified = true;
        }

        ClearPendingOtp();
        Raise(new ApplicantRegistered(Id.Value, IdentityUserId, now));
        return Result.Success();
    }

    public Result AddAcademicRecord(AcademicRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _academicHistory.Add(record);
        return Result.Success();
    }

    public void UpdateProfile(string givenName, string familyName, string? mobile)
    {
        if (!string.IsNullOrWhiteSpace(givenName))
        {
            GivenName = givenName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(familyName))
        {
            FamilyName = familyName.Trim();
        }

        Mobile = string.IsNullOrWhiteSpace(mobile) ? Mobile : mobile.Trim();
    }

    private void ClearPendingOtp()
    {
        PendingOtpHash = null;
        PendingOtpChannel = null;
        PendingOtpExpiresAt = null;
        PendingOtpAttempts = 0;
    }
}
