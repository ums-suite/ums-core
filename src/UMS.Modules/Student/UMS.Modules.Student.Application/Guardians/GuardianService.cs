using System.Text.Json;
using UMS.Modules.Student.Application.Abstractions;
using UMS.Modules.Student.Application.Common;
using UMS.Modules.Student.Domain.Guardians;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Student.Application.Guardians;

/// <summary>
/// Guardian/GuardianAccessGrant scaffolding (docs/ddd/ubiquitous-language.md - not decomposed into
/// tickets.md; this module's own PR description documents the first-pass endpoint/data-shape
/// choices this class implements). Every operation here is Student-owned only - there is
/// deliberately no admin/HR variant, since Guardian consent is the Student's own to give and
/// revoke (glossary: "by explicit Student consent ... revocable by the Student at any time").
/// </summary>
public sealed class GuardianService(
    IStudentRepository students,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    public async Task<Result<IReadOnlyList<GuardianDto>>> ListOwnGuardiansAsync(Guid callerUserId, CancellationToken cancellationToken = default)
    {
        var student = await students.GetByUserIdAsync(callerUserId, cancellationToken).ConfigureAwait(false);
        if (student is null)
        {
            return Error.NotFound("student.not_found", "No Student profile is linked to your account.");
        }

        var guardianDtos = student.Guardians.Select(g => ToDto(g, student.GuardianAccessGrants)).ToList();
        return Result.Success<IReadOnlyList<GuardianDto>>(guardianDtos);
    }

    public async Task<Result<GuardianDto>> LinkGuardianAsync(Guid callerUserId, LinkGuardianRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var student = await students.GetByUserIdAsync(callerUserId, cancellationToken).ConfigureAwait(false);
        if (student is null)
        {
            return Error.NotFound("student.not_found", "No Student profile is linked to your account.");
        }

        Guardian guardian;
        try
        {
            guardian = student.LinkGuardian(request.Name, request.Relationship, request.ContactEmail, request.ContactPhone, clock.UtcNow);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("guardian.invalid", ex.Message);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest(
            "Guardian",
            guardian.Id.Value.ToString(),
            AuditActions.Create,
            null,
            JsonSerializer.Serialize(new { studentId = student.Id.Value, name = guardian.Name, relationship = guardian.Relationship }),
            organizationScopeId: student.DepartmentId);

        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : ToDto(guardian, student.GuardianAccessGrants);
    }

    /// <summary>"No default/implicit visibility ever, every category is a separate explicit grant" - see <see cref="Domain.Students.Student.GrantGuardianAccess"/>'s own remarks.</summary>
    public async Task<Result<GuardianDto>> GrantAccessAsync(Guid callerUserId, Guid guardianId, GrantGuardianAccessRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<GuardianAccessCategory>(request.Category, ignoreCase: true, out var category))
        {
            return Error.Validation("guardian.invalid_category", $"'{request.Category}' is not a valid Guardian access category.");
        }

        var student = await students.GetByUserIdAsync(callerUserId, cancellationToken).ConfigureAwait(false);
        if (student is null)
        {
            return Error.NotFound("student.not_found", "No Student profile is linked to your account.");
        }

        try
        {
            student.GrantGuardianAccess(guardianId, category, clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Conflict("guardian.grant_invalid", ex.Message);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest(
            "GuardianAccessGrant",
            $"{guardianId}:{category}",
            AuditActions.Assign,
            null,
            JsonSerializer.Serialize(new { studentId = student.Id.Value, guardianId, category = category.ToString() }),
            organizationScopeId: student.DepartmentId);

        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (commitResult.IsFailure)
        {
            return commitResult.Error!;
        }

        var guardian = student.Guardians.First(g => g.Id.Value == guardianId);
        return ToDto(guardian, student.GuardianAccessGrants);
    }

    /// <summary>"Student-revocable at any time" - docs/ddd/ubiquitous-language.md.</summary>
    public async Task<Result<GuardianDto>> RevokeAccessAsync(Guid callerUserId, Guid guardianId, string category, AuditContext audit, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<GuardianAccessCategory>(category, ignoreCase: true, out var parsedCategory))
        {
            return Error.Validation("guardian.invalid_category", $"'{category}' is not a valid Guardian access category.");
        }

        var student = await students.GetByUserIdAsync(callerUserId, cancellationToken).ConfigureAwait(false);
        if (student is null)
        {
            return Error.NotFound("student.not_found", "No Student profile is linked to your account.");
        }

        try
        {
            student.RevokeGuardianAccess(guardianId, parsedCategory, clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Conflict("guardian.revoke_invalid", ex.Message);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest(
            "GuardianAccessGrant",
            $"{guardianId}:{parsedCategory}",
            AuditActions.Revoke,
            null,
            JsonSerializer.Serialize(new { studentId = student.Id.Value, guardianId, category = parsedCategory.ToString() }),
            reason: "Revoked by Student.",
            organizationScopeId: student.DepartmentId);

        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (commitResult.IsFailure)
        {
            return commitResult.Error!;
        }

        var guardian = student.Guardians.First(g => g.Id.Value == guardianId);
        return ToDto(guardian, student.GuardianAccessGrants);
    }

    private static GuardianDto ToDto(Guardian guardian, IEnumerable<GuardianAccessGrant> allGrants) => new(
        guardian.Id.Value,
        guardian.StudentId.Value,
        guardian.Name,
        guardian.Relationship,
        guardian.ContactEmail,
        guardian.ContactPhone,
        guardian.LinkedAt,
        allGrants.Where(g => g.GuardianId == guardian.Id.Value && g.IsActive)
            .Select(g => new GuardianAccessGrantDto(g.Id.Value, g.Category.ToString(), g.GrantedAt))
            .ToList());
}
