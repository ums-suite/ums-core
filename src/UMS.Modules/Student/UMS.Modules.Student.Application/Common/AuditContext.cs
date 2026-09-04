using UMS.Shared.Audit;

namespace UMS.Modules.Student.Application.Common;

/// <summary>Bundles the caller-identity fields every mutation call site needs to pass to <see cref="IAuditRecorder"/> - mirrors Faculty's own <c>AuditContext</c> exactly, extended with <see cref="ForSystem"/> for the one call site (<c>IStudentRecordProvisioner</c>, STU-1) with no interactive HTTP user behind it (ADR-0012's own <c>system:&lt;job-name&gt;</c> actor convention, requirement-spec.md audit §8).</summary>
public sealed record AuditContext(Guid ActorUserId, string? ActorIpAddress, string CorrelationId)
{
    private const string CallingApplication = "ums-student-web";
    private const string SystemActorId = "system:student-record-provisioner";

    /// <summary>True only for <see cref="ForSystem"/>-constructed instances - never set by an interactive HTTP endpoint.</summary>
    public bool IsSystemActor { get; private init; }

    /// <summary>STU-1's internal-only <c>CreateStudentRecord</c> call has no interactive HTTP user behind it - the Audit entry records the well-known <c>system:student-record-provisioner</c> principal instead (ADR-0012).</summary>
    public static AuditContext ForSystem(string correlationId) => new(Guid.Empty, null, correlationId) { IsSystemActor = true };

    /// <summary><paramref name="reason"/> is mandatory for a reversal-shaped action - Audit's own <c>AuditLogEntry.Create</c> rejects `reject`/`revoke` (requirement-spec.md audit §4) without one; every other action ignores it.</summary>
    public RecordAuditEntryRequest ToRequest(string entityType, string entityId, string action, string? beforeValueJson, string? afterValueJson, string? reason = null, Guid? organizationScopeId = null) =>
        new(
            ActorId: IsSystemActor ? SystemActorId : ActorUserId.ToString(),
            ActorType: IsSystemActor ? AuditActorType.System : AuditActorType.User,
            IpAddress: ActorIpAddress,
            Application: IsSystemActor ? "system" : CallingApplication,
            EntityType: entityType,
            EntityId: entityId,
            Action: action,
            BeforeValueJson: beforeValueJson,
            AfterValueJson: afterValueJson,
            CorrelationId: CorrelationId,
            Reason: reason,
            OrganizationScopeId: organizationScopeId);
}
