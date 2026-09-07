namespace UMS.Modules.Student.Application.StudentRequests;

public sealed record StudentRequestDto(
    Guid Id,
    Guid StudentId,
    string RequestType,
    string Details,
    string Status,
    Guid? ReviewScopeNodeId,
    bool IsAgainstOwnDepartmentHead,
    Guid? GeneratedDocumentId,
    Guid? DecidedByUserId,
    string? DecisionReason,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? DecidedAt,
    DateTimeOffset? FulfilledAt,
    uint Version);

/// <summary>STU-9 (§6 <c>POST /students/requests</c>, ID reissue).</summary>
public sealed record SubmitIdReissueRequest(string Reason);

/// <summary>STU-10 (transcript request).</summary>
public sealed record SubmitTranscriptRequestRequest(string Purpose);

/// <summary>STU-11 (grievance) - <paramref name="IsAgainstOwnDepartmentHead"/> is the student-declared conflict-of-interest flag edge-cases.md's escalation rule keys off.</summary>
public sealed record SubmitGrievanceRequest(string Description, bool IsAgainstOwnDepartmentHead);

/// <summary>STU-14: reason required (requirement-spec.md §6).</summary>
public sealed record RejectStudentRequestRequest(string Reason, uint Version);

/// <summary>
/// STU-9/STU-10/STU-11: one discriminated body for the spec's single literal
/// <c>POST /students/requests</c> route (requirement-spec.md §6) - <see cref="RequestType"/> is one
/// of Domain's own <c>StudentRequestType</c> names (<c>"IdReissue"</c>/<c>"TranscriptRequest"</c>/
/// <c>"Grievance"</c>), dispatched by the endpoint to the matching <c>StudentRequestService</c>
/// submit method; the Api layer only ever compares this as a plain string (never references
/// Domain's enum directly), mirroring <c>ChangeStudentStatusRequest.Status</c>'s own pattern.
/// </summary>
public sealed record SubmitStudentRequestRequest(string RequestType, string? Reason, string? Purpose, string? Description, bool IsAgainstOwnDepartmentHead);

public sealed record VersionedRequestBody(uint Version);
