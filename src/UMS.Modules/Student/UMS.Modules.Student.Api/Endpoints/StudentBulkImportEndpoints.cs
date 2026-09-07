using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Student.Application.BulkImport;
using UMS.Modules.Student.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Student.Api.Endpoints;

/// <summary>
/// STU-15/STU-16 (requirement-spec.md student §2 Bulk Import, §6). Admin-only
/// (<see cref="StudentPermissions.BulkImportExecute"/>) throughout.
///
/// <para>
/// <b>Documented deviation from the literal 2-endpoint spec table:</b> a third route,
/// <c>POST /students/bulk-import/{jobId}/approve</c>, is added here - §2's own stated flow names
/// "Approve" as an explicit step between "Preview errors" and "Process" that the bare
/// Upload+Poll shape the literal table describes has no way to express, the same class of
/// documented addition Faculty's <c>LeaveRequestEndpoints</c> and Learning's
/// <c>AssignmentEndpoints</c> already made for their own specs.
/// </para>
/// </summary>
internal static class StudentBulkImportEndpoints
{
    public static void MapStudentBulkImportEndpoints(this RouteGroupBuilder group)
    {
        var bulkImport = group.MapGroup("/students/bulk-import");

        bulkImport.MapPost("/", async (UploadStudentBulkImportRequest body, HttpContext httpContext, StudentBulkImportService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UploadAsync(httpContext.User.GetUserId(), body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(
                dto => Results.Created($"/api/v1/student/students/bulk-import/{dto.Id}", dto),
                error => error.ToProblemResult(httpContext));
        }).RequirePermission(StudentPermissions.BulkImportExecute);

        bulkImport.MapPost("/{jobId:guid}/approve", async (Guid jobId, HttpContext httpContext, StudentBulkImportService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ApproveAsync(jobId, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(StudentPermissions.BulkImportExecute);

        bulkImport.MapGet("/{jobId:guid}", async (Guid jobId, HttpContext httpContext, StudentBulkImportService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetReportAsync(jobId, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(StudentPermissions.BulkImportExecute);
    }
}
