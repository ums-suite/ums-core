namespace UMS.Modules.Student.Application.BulkImport;

/// <summary>
/// One input row (requirement-spec.md student §2 Bulk Import). Presence of <see cref="StudentNumber"/>
/// selects the UPDATE path (an existing Student, field-scoped to contact/photo info only -
/// design-decisions.md "Bulk-Import Concurrency &amp; Field-Scoping Design"); its absence selects the
/// CREATE path (a brand-new cohort member, processed through the exact same
/// <c>CreateStudentRecordService.CreateAsync</c> STU-1 already uses, so a bulk-imported Student gets
/// identical idempotency/StudentNumber-generation/side-effect behavior to one created individually).
/// </summary>
/// <param name="ExpectedVersion">UPDATE rows only - the row's own captured `Student.Version` at Upload time, checked as the optimistic-concurrency guard against a concurrent `PUT /students/me` (edge-cases.md, "A bulk-import row upsert races a student's own concurrent self-service profile edit").</param>
public sealed record StudentBulkImportRowInput(
    Guid? OriginatingApplicationId,
    string? StudentNumber,
    uint? ExpectedVersion,
    int? AdmissionYear,
    string? FacultyCode,
    Guid? DepartmentId,
    Guid? ProgramId,
    string? GivenName,
    string? FamilyName,
    string? GivenNameBn,
    string? FamilyNameBn,
    string? Email,
    string? Mobile,
    DateOnly? DateOfBirth,
    string? NationalId,
    string? ContactEmail,
    string? ContactPhone,
    string? PhotoUrl);

/// <summary>STU-15 (§6 <c>POST /students/bulk-import</c>) - the Upload step. Row parsing (e.g. CSV -&gt; this shape) happens client-side/upstream of this API; the endpoint accepts already-structured rows.</summary>
public sealed record UploadStudentBulkImportRequest(IReadOnlyList<StudentBulkImportRowInput> Rows);

public sealed record StudentBulkImportJobDto(
    Guid Id,
    Guid RequestedByUserId,
    string Status,
    int TotalRows,
    int ValidRowCount,
    int InvalidRowCount,
    int ProcessedCount,
    int SucceededCount,
    int FailedCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? CompletedAt);

/// <summary>One row's own outcome, as surfaced by STU-16's report - <see cref="ErrorMessage"/> is the validation failure (Invalid rows) or processing failure (Failed rows); always <see langword="null"/> for a Succeeded row.</summary>
public sealed record StudentBulkImportRowReportDto(int RowNumber, string Status, string? ErrorMessage, Guid? ResultStudentId);

/// <summary>STU-16 (§6 <c>GET /students/bulk-import/{jobId}</c>) - job status plus the full per-row report (doubles as the "Preview errors" view before Approve, and the final report after Process).</summary>
public sealed record StudentBulkImportJobReportDto(StudentBulkImportJobDto Job, IReadOnlyList<StudentBulkImportRowReportDto> Rows);
