using System.Text.Json;
using Microsoft.Extensions.Options;
using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Domain.Users;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Identity.Application.Auth;

/// <summary>
/// IDN-10/IDN-11: TOTP enrollment and verification (requirement-spec.md identity §2 MFA, §9.3).
/// <see cref="VerifyAsync"/> serves two distinct outcomes behind one endpoint (§6 `POST
/// /auth/mfa/verify`): completing enrollment (a still-pending, unverified secret) and completing a
/// mid-login MFA challenge (an already-enrolled secret) - see that method's own remarks for how it
/// tells the two apart and why a single successful enrollment-completion call can also finish the
/// login that triggered it.
/// </summary>
public sealed class MfaEnrollmentService(
    IUserRepository users,
    IMfaSecretEncryptor encryptor,
    ITotpGenerator totp,
    AuthenticationService authenticationService,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock,
    IOptions<IdentityMfaOptions> mfaOptions)
{
    public async Task<Result<MfaEnrollmentDto>> EnrollAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(new UserId(userId), cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Error.NotFound("user.not_found", $"No User exists with id '{userId}'.");
        }

        var secret = totp.GenerateSecret();
        var cipherText = encryptor.Encrypt(secret);
        var now = clock.UtcNow;
        var pendingLifetime = mfaOptions.Value.PendingSecretLifetime;

        user.BeginMfaEnrollment(cipherText, now, pendingLifetime);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var otpAuthUri = totp.BuildOtpAuthUri(secret, user.Email.Value, mfaOptions.Value.Issuer);
        return new MfaEnrollmentDto(totp.EncodeSecretForDisplay(secret), otpAuthUri, now + pendingLifetime);
    }

    /// <summary>
    /// Tries the still-pending (unverified) secret first, if one exists and hasn't expired -
    /// succeeding there completes enrollment (<see cref="User.CompleteMfaEnrollment"/>, raising
    /// `MfaEnrolled`, audited in the same transaction per requirement-spec.md identity §5
    /// Auditability). Falls back to the already-enrolled secret otherwise - the ordinary path once
    /// a User is fully set up. Either branch, when <paramref name="isMfaChallenge"/> is true (the
    /// caller authenticated via IDN-11's mid-login challenge token, not a live session), also
    /// completes the login that issued that challenge - the same code the User just typed doubles
    /// as both "prove enrollment" and "prove this login."
    /// </summary>
    public async Task<Result<MfaVerifyResult>> VerifyAsync(Guid userId, string code, bool isMfaChallenge, string? userAgent, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(new UserId(userId), cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Error.NotFound("user.not_found", $"No User exists with id '{userId}'.");
        }

        var now = clock.UtcNow;

        if (user.Mfa.HasUnexpiredPendingSecret(now))
        {
            var pendingSecret = encryptor.Decrypt(user.Mfa.PendingSecretCipherText!);
            if (!totp.VerifyCode(pendingSecret, code))
            {
                return Error.Unauthorized("auth.mfa_invalid_code", "Invalid MFA code.");
            }

            var completeResult = await CompleteEnrollmentWithAuditAsync(user, now, cancellationToken).ConfigureAwait(false);
            if (completeResult.IsFailure)
            {
                return completeResult.Error!;
            }

            return await FinishAsync(user, isMfaChallenge, userAgent, ipAddress, cancellationToken).ConfigureAwait(false);
        }

        if (user.Mfa.IsEnrolled)
        {
            var enrolledSecret = encryptor.Decrypt(user.Mfa.EnrolledSecretCipherText!);
            if (!totp.VerifyCode(enrolledSecret, code))
            {
                return Error.Unauthorized("auth.mfa_invalid_code", "Invalid MFA code.");
            }

            return await FinishAsync(user, isMfaChallenge, userAgent, ipAddress, cancellationToken).ConfigureAwait(false);
        }

        return Error.Validation("auth.mfa_not_enrolled", "No MFA enrollment is pending or active for this User - call POST /auth/mfa/enroll first.");
    }

    private async Task<Result> CompleteEnrollmentWithAuditAsync(User user, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        user.CompleteMfaEnrollment(now);

        var auditRequest = new RecordAuditEntryRequest(
            ActorId: user.Id.Value.ToString(),
            ActorType: AuditActorType.User,
            IpAddress: null,
            Application: "identity",
            EntityType: "User",
            EntityId: user.Id.Value.ToString(),
            Action: "mfa_enrolled",
            BeforeValueJson: null,
            AfterValueJson: JsonSerializer.Serialize(new { mfaEnrolledAt = now }),
            CorrelationId: Guid.NewGuid().ToString());

        var auditResult = await auditRecorder.RecordEntryAsync(auditRequest, transaction.DbTransaction, cancellationToken).ConfigureAwait(false);
        if (auditResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure(auditResult.Error!);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task<Result<MfaVerifyResult>> FinishAsync(User user, bool isMfaChallenge, string? userAgent, string? ipAddress, CancellationToken cancellationToken)
    {
        if (!isMfaChallenge)
        {
            return new MfaVerifyResult(Tokens: null);
        }

        var activeRoles = await authenticationService.GetActiveRolesAsync(user, cancellationToken).ConfigureAwait(false);
        var tokens = await authenticationService.CompleteLoginAsync(user, activeRoles, userAgent, ipAddress, cancellationToken).ConfigureAwait(false);
        return new MfaVerifyResult(tokens);
    }
}
