namespace UMS.Modules.Library.Domain.Common;

/// <summary>
/// requirement-spec.md §2: a <c>Loan</c> is issued "to a Student or FacultyMember (resolved via
/// Identity)" - every Library aggregate that names a borrower (Loan, Reservation, Fine) carries both
/// the borrower's own id AND this discriminator, since Library depends on both
/// <c>UMS.Shared.Student.IStudentStatusChecker</c> and <c>UMS.Shared.Faculty.IFacultyMemberLookup</c>
/// (module-boundaries.md) and the two id spaces are otherwise indistinguishable Guids.
/// </summary>
public enum BorrowerType
{
    Student,
    Faculty,
}
