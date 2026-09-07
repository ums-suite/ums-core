using System.Security.Cryptography;
using UMS.Modules.Admission.Application.Abstractions;
using UMS.Modules.Admission.Domain.Applicants;
using UMS.Shared.Domain;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Identity;
using UMS.Shared.Notifications;

namespace UMS.Modules.Admission.Application.Applicants;

/// <summary>ADM-2/ADM-3: Applicant Registration &amp; Verification (requirement-spec.md §2, §6).</summary>
public sealed class ApplicantService(
    IApplicantRepository applicants,
    IUserProvisioner userProvisioner,
    INotificationRequestIntake notificationIntake,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    private static readonly TimeSpan OtpTtl = TimeSpan.FromMinutes(10);

    /// <summary>Provisions a real, login-capable Identity User (see <see cref="Applicant"/>'s own remarks) then creates the Applicant profile linked to it.</summary>
    public async Task<Result<ApplicantDto>> RegisterAsync(RegisterApplicantRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var username = request.Email.Trim().ToLowerInvariant();
        var provisioned = await userProvisioner.ProvisionAsync(
            new ProvisionUserCommand(username, request.Email, request.GivenName, request.FamilyName, GivenNameBn: null, FamilyNameBn: null, request.Mobile, UniversityId: null, GenerateInitialPassword()),
            cancellationToken).ConfigureAwait(false);

        if (provisioned.IsFailure)
        {
            return provisioned.Error!;
        }

        var created = Applicant.Register(provisioned.Value.UserId, request.GivenName, request.FamilyName, request.Email, request.Mobile, request.DateOfBirth, clock.UtcNow);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        applicants.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(created.Value);
    }

    /// <summary>ADM-3: issues a fresh OTP and delivers it synchronously through Notifications' real intake - time-sensitive, unlike Admission's own less-urgent fan-out which goes through its own outbox/relay worker (see <c>INotificationRequestPublisher</c>'s own remarks).</summary>
    public async Task<Result> RequestOtpAsync(Guid applicantId, OtpChannel channel, string correlationId, CancellationToken cancellationToken = default)
    {
        var applicant = await applicants.GetByIdAsync(new ApplicantId(applicantId), cancellationToken).ConfigureAwait(false);
        if (applicant is null)
        {
            return Result.Failure(Error.NotFound("applicant.not_found", $"No Applicant exists with id '{applicantId}'."));
        }

        var code = OtpGenerator.GenerateCode();
        applicant.IssueVerificationOtp(channel, OtpGenerator.Hash(applicant.Id.Value, code), clock.UtcNow.Add(OtpTtl));
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var submitted = await notificationIntake.SubmitAsync(
            new SubmitNotificationRequestCommand("admission", "ApplicantOtpIssued", applicant.Id.Value.ToString(), applicant.IdentityUserId, $"{{\"code\":\"{code}\"}}"),
            cancellationToken).ConfigureAwait(false);

        return submitted.IsFailure ? Result.Failure(submitted.Error!) : Result.Success();
    }

    public async Task<Result> VerifyOtpAsync(Guid applicantId, OtpChannel channel, string code, CancellationToken cancellationToken = default)
    {
        var applicant = await applicants.GetByIdAsync(new ApplicantId(applicantId), cancellationToken).ConfigureAwait(false);
        if (applicant is null)
        {
            return Result.Failure(Error.NotFound("applicant.not_found", $"No Applicant exists with id '{applicantId}'."));
        }

        var verified = applicant.VerifyOtp(channel, OtpGenerator.Hash(applicant.Id.Value, code), clock.UtcNow);
        if (verified.IsFailure)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return verified;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<ApplicantDto>> AddAcademicRecordAsync(Guid applicantId, AcademicRecordRequest request, CancellationToken cancellationToken = default)
    {
        var applicant = await applicants.GetByIdAsync(new ApplicantId(applicantId), cancellationToken).ConfigureAwait(false);
        if (applicant is null)
        {
            return Error.NotFound("applicant.not_found", $"No Applicant exists with id '{applicantId}'.");
        }

        var score = request.IsGpaScale ? PercentageOrGpa.CreateGpa(request.Score) : PercentageOrGpa.CreatePercentage(request.Score);
        if (score.IsFailure)
        {
            return score.Error!;
        }

        var record = AcademicRecord.Create(request.Board, request.ExamName, request.PassingYear, score.Value);
        if (record.IsFailure)
        {
            return record.Error!;
        }

        applicant.AddAcademicRecord(record.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(applicant);
    }

    public async Task<Result<ApplicantDto>> GetByIdentityUserIdAsync(Guid identityUserId, CancellationToken cancellationToken = default)
    {
        var applicant = await applicants.GetByIdentityUserIdAsync(identityUserId, cancellationToken).ConfigureAwait(false);
        return applicant is null
            ? Error.NotFound("applicant.not_found", $"No Applicant exists for Identity User '{identityUserId}'.")
            : ToDto(applicant);
    }

    internal static ApplicantDto ToDto(Applicant applicant) => new(
        applicant.Id.Value,
        applicant.IdentityUserId,
        applicant.GivenName,
        applicant.FamilyName,
        applicant.Email,
        applicant.Mobile,
        applicant.DateOfBirth,
        applicant.IsEmailVerified,
        applicant.IsMobileVerified);

    private static string GenerateInitialPassword() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
}
