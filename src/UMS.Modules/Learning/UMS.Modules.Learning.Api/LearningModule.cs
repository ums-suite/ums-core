using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Learning.Api.Endpoints;

namespace UMS.Modules.Learning.Api;

/// <summary>Learning's endpoints register themselves under <c>/api/v1/learning/...</c> (release/DEVELOPMENT_PLAN.md Flow #13; requirement-spec.md learning §6), mirroring Academic's own module registration exactly.</summary>
public static class LearningModule
{
    public static IEndpointRouteBuilder MapLearningModule(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/v1/learning").WithTags("Learning");

        group.MapAssignmentEndpoints();
        group.MapSubmissionEndpoints();
        group.MapLectureMaterialEndpoints();
        group.MapDiscussionEndpoints();

        return endpoints;
    }
}
