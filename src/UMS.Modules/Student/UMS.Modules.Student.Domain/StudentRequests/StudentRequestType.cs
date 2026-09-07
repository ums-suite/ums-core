namespace UMS.Modules.Student.Domain.StudentRequests;

/// <summary>STU-9/STU-10/STU-11: the three request kinds requirement-spec.md student §2 Student-Initiated Requests names, each with its own downstream fulfillment path.</summary>
public enum StudentRequestType
{
    IdReissue = 0,
    TranscriptRequest = 1,
    Grievance = 2,
}
