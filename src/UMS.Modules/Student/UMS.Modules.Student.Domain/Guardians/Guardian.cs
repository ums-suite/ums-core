using UMS.Modules.Student.Domain.Students;

namespace UMS.Modules.Student.Domain.Guardians;

/// <summary>
/// docs/ddd/ubiquitous-language.md: "A parent/guardian linked to one or more Students by explicit
/// Student consent, holding a scoped, read-only view (fees, attendance, grades) via its own
/// limited Identity login - never a Student proxy with write access." Kind: Entity, not Aggregate
/// root.
///
/// <para>
/// <b>First-pass scaffolding decision (documented in this module's own PR description, since
/// Guardian/GuardianAccessGrant are not decomposed into tickets.md):</b> modeled here as a child
/// entity of <c>Student</c> (this <see cref="StudentId"/> field), not as its own separately shared
/// aggregate root. The glossary's "linked to one or more Students" phrasing suggests the same
/// physical person could in principle link to several sibling Students; deduplicating that into one
/// cross-Student Guardian identity is deferred until Guardian actually needs its own Identity login
/// (glossary: "via its own limited Identity login" - future <c>ums-student-web</c> Guardian View
/// work, release/DEVELOPMENT_PLAN.md Flow #21) - until then, a Guardian who links to two sibling
/// Students today is simply two rows, one under each Student, which is the correct and simplest
/// model for "consent scoped per Student" (a Guardian's access to one sibling says nothing about
/// their access to another).
/// </para>
/// </summary>
public sealed class Guardian
{
    private Guardian()
    {
    }

    private Guardian(GuardianId id, StudentId studentId, string name, string relationship, string? contactEmail, string? contactPhone, DateTimeOffset linkedAt)
    {
        Id = id;
        StudentId = studentId;
        Name = name;
        Relationship = relationship;
        ContactEmail = contactEmail;
        ContactPhone = contactPhone;
        LinkedAt = linkedAt;
    }

    public GuardianId Id { get; private set; }

    /// <summary>
    /// Same CLR type as the owning <c>Student.Id</c> (not a bare <c>Guid</c>) on purpose - EF
    /// Core's owned-collection foreign key must match its principal key's exact type, and Student's
    /// own <c>StudentId</c> id-conversion (<c>StudentConfiguration</c>) applies identically here.
    /// </summary>
    public StudentId StudentId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Free text (e.g. "Father", "Mother", "Legal Guardian") - the BRD/glossary does not enumerate a fixed relationship vocabulary.</summary>
    public string Relationship { get; private set; } = string.Empty;

    public string? ContactEmail { get; private set; }

    public string? ContactPhone { get; private set; }

    public DateTimeOffset LinkedAt { get; private set; }

    public static Guardian Link(StudentId studentId, string name, string relationship, string? contactEmail, string? contactPhone, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Guardian name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(relationship))
        {
            throw new ArgumentException("Guardian relationship is required.", nameof(relationship));
        }

        if (string.IsNullOrWhiteSpace(contactEmail) && string.IsNullOrWhiteSpace(contactPhone))
        {
            throw new ArgumentException("At least one of contact email or contact phone is required to link a Guardian.", nameof(contactEmail));
        }

        return new Guardian(
            GuardianId.New(),
            studentId,
            name.Trim(),
            relationship.Trim(),
            string.IsNullOrWhiteSpace(contactEmail) ? null : contactEmail.Trim(),
            string.IsNullOrWhiteSpace(contactPhone) ? null : contactPhone.Trim(),
            now);
    }

    public void UpdateContactInfo(string? contactEmail, string? contactPhone)
    {
        ContactEmail = string.IsNullOrWhiteSpace(contactEmail) ? null : contactEmail.Trim();
        ContactPhone = string.IsNullOrWhiteSpace(contactPhone) ? null : contactPhone.Trim();
    }
}
