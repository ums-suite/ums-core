namespace UMS.Modules.Admission.Application.Applicants;

public sealed record ApplicantDto(
    Guid Id,
    Guid IdentityUserId,
    string GivenName,
    string FamilyName,
    string Email,
    string? Mobile,
    DateOnly DateOfBirth,
    bool IsEmailVerified,
    bool IsMobileVerified);

public sealed record RegisterApplicantRequest(string GivenName, string FamilyName, string Email, string? Mobile, DateOnly DateOfBirth);

public sealed record AcademicRecordRequest(string Board, string ExamName, int PassingYear, decimal Score, bool IsGpaScale);
