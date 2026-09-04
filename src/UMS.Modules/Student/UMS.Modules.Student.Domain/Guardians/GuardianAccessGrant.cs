using UMS.Modules.Student.Domain.Students;

namespace UMS.Modules.Student.Domain.Guardians;

/// <summary>
/// docs/ddd/ubiquitous-language.md: "the consent record binding a Guardian to a specific Student
/// and the specific data categories consented to, revocable by the Student at any time." Kind:
/// Entity.
///
/// <para>
/// <b>First-pass scaffolding decision:</b> revocation sets <see cref="RevokedAt"/> rather than
/// deleting the row - a consent grant/revocation pair is itself a meaningful history (mirroring
/// this module's own append-only <c>StudentStatusHistoryEntry</c> posture, and the platform-wide
/// "prefer append-only over hard delete for a sensitive/consent-shaped record" convention
/// AuditLogEntry/StudentStatusHistory both already apply). At most one ACTIVE (non-revoked) grant
/// exists per <c>(GuardianId, Category)</c> at a time - enforced by <see cref="Student"/>'s own
/// <c>GrantGuardianAccess</c>, never by an external service re-implementing the rule.
/// </para>
/// </summary>
public sealed class GuardianAccessGrant
{
    private GuardianAccessGrant()
    {
    }

    private GuardianAccessGrant(GuardianAccessGrantId id, Guid guardianId, StudentId studentId, GuardianAccessCategory category, DateTimeOffset grantedAt)
    {
        Id = id;
        GuardianId = guardianId;
        StudentId = studentId;
        Category = category;
        GrantedAt = grantedAt;
    }

    public GuardianAccessGrantId Id { get; private set; }

    public Guid GuardianId { get; private set; }

    /// <summary>Same CLR type as the owning <c>Student.Id</c> (not a bare <c>Guid</c>) - see <see cref="Guardian.StudentId"/>'s own remarks for why.</summary>
    public StudentId StudentId { get; private set; }

    public GuardianAccessCategory Category { get; private set; }

    public DateTimeOffset GrantedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsActive => RevokedAt is null;

    public static GuardianAccessGrant Create(Guid guardianId, StudentId studentId, GuardianAccessCategory category, DateTimeOffset now) =>
        new(GuardianAccessGrantId.New(), guardianId, studentId, category, now);

    public void Revoke(DateTimeOffset now)
    {
        if (RevokedAt is not null)
        {
            throw new InvalidOperationException("This GuardianAccessGrant has already been revoked.");
        }

        RevokedAt = now;
    }
}
