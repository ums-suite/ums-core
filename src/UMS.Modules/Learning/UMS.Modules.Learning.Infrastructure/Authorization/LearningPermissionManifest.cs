using UMS.Modules.Learning.Application.Permissions;
using UMS.Shared.Authorization;

namespace UMS.Modules.Learning.Infrastructure.Authorization;

/// <summary>Learning's own contribution to the platform-wide Permission catalog (requirement-spec.md learning §6). Mirrors Academic's/Documents'/Faculty's own PermissionManifest exactly.</summary>
internal sealed class LearningPermissionManifest : IPermissionManifest
{
    public string OwningModule => "learning";

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } =
    [
        new(LearningPermissions.AssignmentManage, "Create, publish, close, or cancel an Assignment (Instructor, additionally ownership-checked against the CourseOffering)."),
        new(LearningPermissions.AssignmentExtensionGrant, "Grant a per-Student SubmissionExtension (accommodation) - audited."),
        new(LearningPermissions.SubmissionReadBatch, "Read an Assignment's whole Submission queue (Instructor's grading queue)."),
        new(LearningPermissions.SubmissionEvaluate, "Record an AssignmentScore on a Submission, and read/retry its PlagiarismCheck."),
        new(LearningPermissions.LectureMaterialManage, "Publish LectureMaterial and its versions against a CourseOffering."),
        new(LearningPermissions.DiscussionModerate, "Remove or restore a DiscussionPost - audited."),
    ];
}
