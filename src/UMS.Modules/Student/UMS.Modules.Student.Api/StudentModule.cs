using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Student.Api.Endpoints;

namespace UMS.Modules.Student.Api;

/// <summary>Student's endpoints register themselves under <c>/api/v1/student/...</c> (release/DEVELOPMENT_PLAN.md Flow #11), mirroring Faculty's own <c>FacultyModule</c> exactly. <c>CreateStudentRecord</c> (STU-1) has NO route here at all - requirement-spec.md §6 states it explicitly as internal-only (see <c>UMS.Shared.Student.IStudentRecordProvisioner</c>).</summary>
public static class StudentModule
{
    public static IEndpointRouteBuilder MapStudentModule(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/student").WithTags("Student");

        group.MapStudentProfileEndpoints();
        group.MapStudentStatusEndpoints();
        group.MapGuardianEndpoints();
        group.MapStudentRequestEndpoints();
        group.MapStudentBulkImportEndpoints();

        return endpoints;
    }
}
