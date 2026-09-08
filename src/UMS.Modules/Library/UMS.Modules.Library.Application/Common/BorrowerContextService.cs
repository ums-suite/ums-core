using UMS.Modules.Library.Domain.Common;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Faculty;
using UMS.Shared.Student;

namespace UMS.Modules.Library.Application.Common;

/// <summary>
/// requirement-spec.md §2: a borrower is "a Student or FacultyMember (resolved via Identity)" - this
/// service turns the caller's own Identity <c>UserId</c> (their JWT's `sub` claim) into a
/// (<c>BorrowerId</c>, <see cref="BorrowerType"/>) pair, trying Student first (mirrors Hostel's own
/// <c>StudentContextService.ResolveOwnStudentIdAsync</c>) then Faculty
/// (<c>UMS.Shared.Faculty.IFacultyMemberLookup</c>) - the two lookups are cheap, already-in-process
/// calls, and a real person is never registered as both.
/// </summary>
public sealed class BorrowerContextService(IStudentStatusChecker studentStatusChecker, IFacultyMemberLookup facultyMemberLookup)
{
    public async Task<Result<BorrowerContext>> ResolveOwnBorrowerAsync(Guid identityUserId, CancellationToken cancellationToken = default)
    {
        var standing = await studentStatusChecker.GetByUserIdAsync(identityUserId, cancellationToken).ConfigureAwait(false);
        if (standing is not null)
        {
            return new BorrowerContext(standing.StudentId, BorrowerType.Student, standing.Status);
        }

        var facultyMember = await facultyMemberLookup.GetByUserIdAsync(identityUserId, cancellationToken).ConfigureAwait(false);
        if (facultyMember is not null)
        {
            return new BorrowerContext(facultyMember.Id, BorrowerType.Faculty, facultyMember.Status);
        }

        return Error.Forbidden("library.no_borrower_record", "The calling user has no Student or FacultyMember record.");
    }

    /// <summary>design-decisions.md's borrower-eligibility gate: the borrower must currently be `Active`, re-resolved fresh inside the issuance transaction (never trusted from an earlier read).</summary>
    public async Task<Result<string>> GetCurrentStatusAsync(Guid borrowerId, BorrowerType borrowerType, CancellationToken cancellationToken = default)
    {
        if (borrowerType == BorrowerType.Student)
        {
            var standing = await studentStatusChecker.GetByStudentIdAsync(borrowerId, cancellationToken).ConfigureAwait(false);
            return standing is null
                ? Error.NotFound("library.student_not_found", $"No Student exists with id '{borrowerId}'.")
                : standing.Status;
        }

        var facultyMember = await facultyMemberLookup.GetAsync(borrowerId, cancellationToken).ConfigureAwait(false);
        return facultyMember is null
            ? Error.NotFound("library.faculty_member_not_found", $"No FacultyMember exists with id '{borrowerId}'.")
            : facultyMember.Status;
    }

    /// <summary>LIB-13: resolves the borrower's own Identity UserId (the Invoice owner Finance's CreateInvoice command requires) - distinct from <see cref="ResolveOwnBorrowerAsync"/>, which goes the other direction (UserId -> borrower).</summary>
    public async Task<Guid?> ResolveIdentityUserIdAsync(Guid borrowerId, BorrowerType borrowerType, CancellationToken cancellationToken = default)
    {
        if (borrowerType == BorrowerType.Student)
        {
            var standing = await studentStatusChecker.GetByStudentIdAsync(borrowerId, cancellationToken).ConfigureAwait(false);
            return standing?.IdentityUserId;
        }

        // UMS.Shared.Faculty.FacultyMemberSummary carries no IdentityUserId field today - a
        // documented gap this build does not extend UMS.Shared.Faculty to close (out of this
        // module's own scope). Faculty fine-settlement therefore falls back to the FacultyMemberId
        // itself as the Invoice owner reference, mirroring how several other cross-module contracts
        // in this codebase accept a "best available identifier" until the owning module's own
        // contract is extended.
        return borrowerId;
    }

    /// <summary>
    /// Used by the Notifications relay worker for outbox events that carry only a bare
    /// <c>BorrowerId</c> Guid, no <see cref="BorrowerType"/> discriminator (e.g. <c>LoanReturned</c>,
    /// <c>FineAccrued</c>) - tries Student first, then falls back to the same
    /// best-available-identifier posture <see cref="ResolveIdentityUserIdAsync"/> documents for
    /// Faculty. Returns <see langword="null"/> if neither lookup resolves the id at all.
    /// </summary>
    public async Task<Guid?> ResolveRecipientUserIdAsync(Guid borrowerId, CancellationToken cancellationToken = default)
    {
        var standing = await studentStatusChecker.GetByStudentIdAsync(borrowerId, cancellationToken).ConfigureAwait(false);
        if (standing is not null)
        {
            return standing.IdentityUserId;
        }

        var facultyMember = await facultyMemberLookup.GetAsync(borrowerId, cancellationToken).ConfigureAwait(false);
        return facultyMember is not null ? borrowerId : null;
    }
}

/// <param name="Status">The borrower's CURRENT standing, resolved fresh at lookup time - never cached/trusted from an earlier read (design-decisions.md).</param>
public sealed record BorrowerContext(Guid BorrowerId, BorrowerType BorrowerType, string Status);
