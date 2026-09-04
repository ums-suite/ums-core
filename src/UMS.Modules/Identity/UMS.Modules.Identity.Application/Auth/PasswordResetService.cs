using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Domain.Users;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Notifications;

namespace UMS.Modules.Identity.Application.Auth;

/// <summary>
/// IDN-12: password forgot/reset (requirement-spec.md identity §2 Credential Security, §6 both
/// endpoints). Never emails/SMSes anything itself (ADR-0009) - delivery is exclusively
/// <see cref="INotificationRequestIntake"/>, Notifications' own real cross-module contract
/// (release/DEVELOPMENT_PLAN.md Flow #8).
/// </summary>
public sealed class PasswordResetService(
    IUserRepository users,
    ISessionRepository sessions,
    IPasswordHasher passwordHasher,
    IPasswordResetTokenService resetTokens,
    INotificationRequestIntake notifications,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock,
    IOptions<IdentityPasswordResetOptions> resetOptions,
    ILogger<PasswordResetService> logger)
{
    private const string SourceModule = "identity";

    /// <summary>
    /// Always succeeds from the caller's point of view regardless of whether
    /// <paramref name="identifier"/> resolves to a real User (requirement-spec.md identity §5
    /// Security NFR's generic-failure posture - never confirms or denies account existence).
    /// </summary>
    public async Task ForgotAsync(string identifier, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdentifierAsync(identifier, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return;
        }

        var now = clock.UtcNow;
        var (plainText, hash) = resetTokens.IssueToken();

        // edge-cases.md, "Concurrent password-reset requests": issuing a new challenge
        // unconditionally replaces the User's single outstanding one (User.IssuePasswordResetChallenge
        // itself has no "if one already exists" branch) - never two simultaneously valid tokens.
        user.IssuePasswordResetChallenge(hash, now, resetOptions.Value.TokenLifetime);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var payload = JsonSerializer.Serialize(new Dictionary<string, string> { ["resetToken"] = plainText });
        var result = await notifications.SubmitAsync(
            new SubmitNotificationRequestCommand(SourceModule, "PasswordResetRequested", user.Id.Value.ToString(), user.Id.Value, payload),
            cancellationToken).ConfigureAwait(false);

        if (result.IsFailure && logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning("Notifications rejected the password-reset request for {UserId}: {Error}", user.Id, result.Error);
        }
    }

    public async Task<Result> ResetAsync(string token, string newPassword, CancellationToken cancellationToken = default)
    {
        var presentedHash = resetTokens.Hash(token);
        var user = await users.GetByPasswordResetTokenHashAsync(presentedHash, cancellationToken).ConfigureAwait(false);

        var now = clock.UtcNow;
        if (user is null || !user.TryConsumePasswordResetChallenge(presentedHash, now))
        {
            // Never distinguishes "no such token" from "expired"/"already used" (identity §5).
            return Result.Failure(Error.Unauthorized("auth.invalid_reset_token", "This password reset link is invalid or has expired."));
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        user.ChangePassword(Credential.FromHash(passwordHasher.HashPassword(newPassword), passwordHasher.AlgorithmName, now), now);

        // edge-cases.md, "Password reset requested for a locked-out account": "must still succeed
        // ... is often exactly how a locked-out legitimate user recovers" - clearing the lockout is
        // this recovery, not a separate administrative action.
        user.ClearLockout();

        var auditRequest = new RecordAuditEntryRequest(
            ActorId: user.Id.Value.ToString(),
            ActorType: AuditActorType.User,
            IpAddress: null,
            Application: "identity",
            EntityType: "User",
            EntityId: user.Id.Value.ToString(),
            Action: "password_reset",
            BeforeValueJson: null,
            AfterValueJson: null,
            CorrelationId: Guid.NewGuid().ToString());

        var auditResult = await auditRecorder.RecordEntryAsync(auditRequest, transaction.DbTransaction, cancellationToken).ConfigureAwait(false);
        if (auditResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure(auditResult.Error!);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        // A password reset is a strong signal the prior credential may have been compromised - log
        // out every device rather than leave old sessions valid under the new password (the same
        // "log out everywhere" mechanism IDN-8 already exposes to the User directly).
        var activeSessions = await sessions.ListActiveForUserAsync(user.Id, cancellationToken).ConfigureAwait(false);
        foreach (var session in activeSessions)
        {
            session.Revoke("password_reset", now);
        }

        if (activeSessions.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }
}
