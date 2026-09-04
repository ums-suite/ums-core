namespace UMS.Modules.Student.Domain.Guardians;

/// <summary>
/// docs/ddd/ubiquitous-language.md, <c>GuardianAccessGrant</c>: "the consent record binding a
/// Guardian to a specific Student and the specific data categories consented to". The three
/// categories the glossary's own <c>Guardian</c> entry names verbatim ("a scoped, read-only view
/// (fees, attendance, grades)") - no default/implicit category is ever granted, every one of these
/// requires its own explicit <see cref="GuardianAccessGrant"/> row (this module's own PR
/// description documents this as a first-pass scaffolding decision, since Guardian/
/// GuardianAccessGrant are not decomposed into tickets.md).
/// </summary>
public enum GuardianAccessCategory
{
    Fees,
    Attendance,
    Grades,
}
