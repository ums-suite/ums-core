namespace UMS.Modules.Learning.Application.Permissions;

/// <summary>
/// requirement-spec.md learning §6's Permission strings (ADR-0006).
///
/// <para>
/// <b>Documented deviation from §6's literal table.</b> That table lists flat two-segment strings
/// (<c>assignment.manage</c>, <c>submission.evaluate</c>, ...). Identity's already-built Permission
/// validator requires the platform's three-segment <c>&lt;module&gt;.&lt;resource&gt;.&lt;action&gt;</c>
/// shape, so every string here is prefixed with its owning module - the exact adaptation Academic
/// (<c>AcademicPermissions</c>) and Documents (<c>DocumentPermissions</c>) both already made and
/// documented for the identical reason. The capability set itself is unchanged from the spec's own
/// table; only the naming shape is normalized. <see cref="AssignmentExtensionGrant"/> keeps four
/// segments for the same reason Academic's own <c>academic.student.result.read</c> does - the
/// resource genuinely is two words there.
/// </para>
/// </summary>
public static class LearningPermissions
{
    public const string AssignmentManage = "learning.assignment.manage";
    public const string AssignmentExtensionGrant = "learning.assignment.extension.grant";
    public const string SubmissionReadBatch = "learning.submission.read.batch";
    public const string SubmissionEvaluate = "learning.submission.evaluate";
    public const string LectureMaterialManage = "learning.lecturematerial.manage";
    public const string DiscussionModerate = "learning.discussion.moderate";
}
