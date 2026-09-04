namespace UMS.Modules.Documents.Application.Verification;

/// <summary>Deliberately does not expose <c>OwnerId</c> as a bare cross-module id (§2 calls for "owner name", which requires a calling module's own display-name resolution Documents doesn't have) or any storage/download reference (§2: "never the raw artifact bytes").</summary>
public sealed record DocumentVerificationDto(
    string DigitalVerificationId,
    string DocumentType,
    string Status,
    bool IsValid,
    string? Reason,
    DateTimeOffset IssuedAt,
    DateTimeOffset? ReadyAt);
