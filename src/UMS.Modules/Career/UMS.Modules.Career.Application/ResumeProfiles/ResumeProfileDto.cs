namespace UMS.Modules.Career.Application.ResumeProfiles;

public sealed record ResumeProfileDto(Guid Id, Guid StudentId, string Label, Guid ArtifactId, string FileName, bool IsDefault, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

/// <summary>Step 1 of CAR-5's upload flow: a presigned-upload slot request against Documents' own `IUploadedArtifactRequester` (requirement-spec.md §7.1).</summary>
public sealed record RequestResumeUploadRequest(string MimeType);

public sealed record ResumeUploadSlotDto(Guid ArtifactId, string? UploadUrl);

/// <summary>
/// Step 2: finalizes a `ResumeProfile` against an artifact the Student's browser already PUT the
/// bytes to. edge-cases.md "ResumeProfile submission racing storage confirmation": this call
/// synchronously re-confirms the artifact reached Documents' own `Ready`-equivalent state before the
/// `ResumeProfile` row is created - the upload is treated as synchronous from the Student's own
/// perspective, mirroring Documents' single-artifact latency-sensitive path.
/// </summary>
/// <param name="Label">e.g. "General," "Software Internship - tailored" (requirement-spec.md §2.7).</param>
/// <param name="IsDefault">requirement-spec.md §2.7: exactly one default per Student at any time.</param>
public sealed record CreateResumeProfileRequest(string Label, Guid ArtifactId, bool IsDefault);

public sealed record UpdateResumeProfileRequest(string Label, bool IsDefault, Guid? NewArtifactId);
