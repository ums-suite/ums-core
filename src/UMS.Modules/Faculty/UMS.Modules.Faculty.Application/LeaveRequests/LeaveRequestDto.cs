namespace UMS.Modules.Faculty.Application.LeaveRequests;

public sealed record LeaveRequestDto(
    Guid Id,
    Guid FacultyMemberId,
    Guid RequesterUserId,
    DateOnly StartDate,
    DateOnly EndDate,
    string Reason,
    string LocalizedReason,
    string Status,
    bool RoutedDirectlyToAuthority,
    Guid? SupportingDocumentReference,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? DecidedAt,
    uint Version);

public sealed record SubmitLeaveRequestRequest(Guid FacultyMemberId, DateOnly StartDate, DateOnly EndDate, string Reason, IReadOnlyDictionary<string, string>? ReasonTranslations);

public sealed record RejectLeaveRequestRequest(string? Reason, uint Version);

public sealed record VersionedRequestBody(uint Version);

public sealed record AttachSupportingDocumentRequest(Guid GeneratedDocumentId, uint Version);

public sealed record LeaveRequestListPage(IReadOnlyList<LeaveRequestDto> Items, int Skip, int Take);
