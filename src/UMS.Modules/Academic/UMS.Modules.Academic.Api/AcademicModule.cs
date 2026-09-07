using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Academic.Api.Endpoints;

namespace UMS.Modules.Academic.Api;

/// <summary>Academic's endpoints register themselves under <c>/api/v1/academic/...</c> (release/DEVELOPMENT_PLAN.md Flow #12), mirroring Faculty/Student's own module registration exactly.</summary>
public static class AcademicModule
{
    public static IEndpointRouteBuilder MapAcademicModule(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/academic").WithTags("Academic");

        group.MapProgramEndpoints();
        group.MapCurriculumEndpoints();
        group.MapCourseEndpoints();
        group.MapAcademicSessionEndpoints();
        group.MapCourseOfferingEndpoints();
        group.MapEnrollmentEndpoints();
        group.MapAttendanceEndpoints();
        group.MapGradeEndpoints();
        group.MapResultPublicationEndpoints();
        group.MapStudentResultEndpoints();

        return endpoints;
    }
}
