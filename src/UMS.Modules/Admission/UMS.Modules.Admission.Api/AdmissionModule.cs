using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Admission.Api.Endpoints;

namespace UMS.Modules.Admission.Api;

/// <summary>Admission's endpoints register themselves under <c>/api/v1/admission/...</c> (release/DEVELOPMENT_PLAN.md Flow #15; requirement-spec.md §6), mirroring every other module's own registration exactly.</summary>
public static class AdmissionModule
{
    public static IEndpointRouteBuilder MapAdmissionModule(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/v1/admission").WithTags("Admission");

        group.MapCampaignEndpoints();
        group.MapApplicantEndpoints();
        group.MapApplicationEndpoints();
        group.MapAdmissionTestEndpoints();
        group.MapExamAttemptEndpoints();
        group.MapMeritListEndpoints();
        group.MapResultEndpoints();

        return endpoints;
    }
}
