namespace UMS.Modules.Learning.Domain.Assignments;

/// <summary>requirement-spec.md learning §2 Submission: "text content, one or more files (via Documents, §7), or both, per the Assignment's configured allowedSubmissionType".</summary>
public enum AllowedSubmissionType
{
    Text,
    File,
    TextOrFile,
}
