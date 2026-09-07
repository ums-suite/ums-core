namespace UMS.Modules.Learning.Domain.Submissions;

/// <summary>requirement-spec.md learning §4: a Submission is never mutated in place; a resubmission creates a new row and marks the previous one <see cref="Superseded"/>. Only the latest non-superseded Submission is graded/plagiarism-checked, but every prior attempt stays permanently retrievable.</summary>
public enum SubmissionStatus
{
    Submitted,
    Superseded,
}
