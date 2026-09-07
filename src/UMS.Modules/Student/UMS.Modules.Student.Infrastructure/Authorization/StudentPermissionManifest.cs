using UMS.Modules.Student.Application.Permissions;
using UMS.Shared.Authorization;

namespace UMS.Modules.Student.Infrastructure.Authorization;

/// <summary>Student's own contribution to the platform-wide Permission catalog (requirement-spec.md student §6). Mirrors Faculty's own <c>FacultyPermissionManifest</c> exactly.</summary>
internal sealed class StudentPermissionManifest : IPermissionManifest
{
    public string OwningModule => "student";

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } =
    [
        new(StudentPermissions.ProfileRead, "Read any Student's profile by id (admin/Registrar)."),
        new(StudentPermissions.StatusChange, "Change a Student's status (admin/Registrar) - audited, reason required."),
        new(StudentPermissions.RequestReview, "Review (approve/reject) a StudentRequest (Department Head/Registrar) - scope-checked for Grievance requests."),
        new(StudentPermissions.BulkImportExecute, "Execute and monitor the async bulk student import job (admin-only)."),
    ];
}
