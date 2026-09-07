using System.Text.Json;
using UMS.Modules.Academic.Application.Abstractions;
using UMS.Modules.Academic.Application.Common;
using UMS.Modules.Academic.Domain.Programs;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Organization;

namespace UMS.Modules.Academic.Application.Programs;

/// <summary>ACD-1 (bundled foundation): Program create/read - requirement-spec.md §7 (Organization Department existence check).</summary>
public sealed class ProgramService(
    IProgramRepository programs,
    IUnitOfWork unitOfWork,
    IOrganizationNodeExistenceChecker departmentChecker,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    public async Task<Result<ProgramDto>> CreateAsync(CreateProgramRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        if (!await departmentChecker.ExistsAsync(request.DepartmentId, cancellationToken).ConfigureAwait(false))
        {
            return Error.Validation("program.department_not_found", $"No Organization Department exists with id '{request.DepartmentId}'.");
        }

        Domain.Programs.Program program;
        try
        {
            program = Domain.Programs.Program.Create(request.DepartmentId, request.Code, request.Name, request.MaxCreditsPerSemester, request.RequiresAdvisorApproval, clock.UtcNow);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("program.invalid", ex.Message);
        }

        programs.Add(program);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest("Program", program.Id.Value.ToString(), "create", null, JsonSerializer.Serialize(ToDto(program)), organizationScopeId: request.DepartmentId);
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : ToDto(program);
    }

    public async Task<Result<ProgramDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var program = await programs.GetByIdAsync(new ProgramId(id), cancellationToken).ConfigureAwait(false);
        return program is null ? Error.NotFound("program.not_found", $"No Program exists with id '{id}'.") : ToDto(program);
    }

    internal static ProgramDto ToDto(Domain.Programs.Program program) =>
        new(program.Id.Value, program.DepartmentId, program.Code, program.Name, program.MaxCreditsPerSemester, program.RequiresAdvisorApproval, program.CreatedAt);
}
