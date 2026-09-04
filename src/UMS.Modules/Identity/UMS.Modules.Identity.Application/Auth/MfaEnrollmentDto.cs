namespace UMS.Modules.Identity.Application.Auth;

/// <summary>IDN-10's <c>POST /auth/mfa/enroll</c> response - <paramref name="SecretBase32"/> is shown once, for manual entry, alongside the QR-renderable <paramref name="OtpAuthUri"/>; never persisted or returned again.</summary>
public sealed record MfaEnrollmentDto(string SecretBase32, string OtpAuthUri, DateTimeOffset ExpiresAt);

public sealed record MfaVerifyRequest(string Code);

/// <summary>IDN-11's <c>POST /auth/mfa/verify</c> response - <paramref name="Tokens"/> is populated only when this call completed a mid-login MFA challenge; null for a proactive enrollment-only verify from an already-live session.</summary>
public sealed record MfaVerifyResult(TokenPairResult? Tokens);
