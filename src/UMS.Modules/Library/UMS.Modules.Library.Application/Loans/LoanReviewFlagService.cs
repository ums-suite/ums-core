using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Application.Common;
using UMS.Modules.Library.Domain.Common;
using UMS.Modules.Library.Domain.Loans;

namespace UMS.Modules.Library.Application.Loans;

/// <summary>
/// LIB-16: requirement-spec.md §8 edge case "A Faculty member's employment status changes to
/// inactive while holding a Loan → flagged for a recall notice, not auto-returned (a human decision,
/// since the physical copy is still out)" - and the identical treatment for a Student. Mirrors
/// Hostel's own <c>AllocationReviewFlagService</c> exactly, extended to cover BOTH status-change
/// sources this module depends on (Student AND Faculty), since a Loan's borrower can be either.
///
/// <para>
/// Deliberately checks <c>Status != "Active"</c> rather than hardcoding an explicit list of
/// "flag-worthy" status names (Hostel's own precedent hardcodes <c>["Suspended", "Graduated"]</c> for
/// Student alone) - Student's and Faculty's own status vocabularies differ
/// (<c>Suspended</c>/<c>Graduated</c>/<c>Transferred</c> vs. <c>OnLeave</c>/<c>Suspended</c>/
/// <c>Separated</c>) and requirement-spec.md's own wording ("no-longer-active borrower") is itself
/// generic across both - a single "not Active" check covers either module's real status vocabulary
/// without this service needing to enumerate and keep in sync with two different modules' own enums.
/// </para>
/// </summary>
public sealed class LoanReviewFlagService(
    ILoanRepository loans,
    ILoanReviewFlagRepository reviewFlags,
    BorrowerContextService borrowerContext,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public Task ApplyStudentStatusChangeAsync(Guid studentId, string sourceEventReference, CancellationToken cancellationToken = default) =>
        ApplyStatusChangeAsync(studentId, BorrowerType.Student, sourceEventReference, cancellationToken);

    public Task ApplyFacultyStatusChangeAsync(Guid facultyMemberId, string sourceEventReference, CancellationToken cancellationToken = default) =>
        ApplyStatusChangeAsync(facultyMemberId, BorrowerType.Faculty, sourceEventReference, cancellationToken);

    public async Task<IReadOnlyList<LoanReviewFlagDto>> GetByLoanAsync(Guid loanId, CancellationToken cancellationToken = default) =>
        (await reviewFlags.GetByLoanAsync(loanId, cancellationToken).ConfigureAwait(false))
            .Select(f => new LoanReviewFlagDto(f.Id, f.LoanId, f.Reason, f.SourceEventReference, f.CreatedAt))
            .ToList();

    private async Task ApplyStatusChangeAsync(Guid borrowerId, BorrowerType borrowerType, string sourceEventReference, CancellationToken cancellationToken)
    {
        var status = await borrowerContext.GetCurrentStatusAsync(borrowerId, borrowerType, cancellationToken).ConfigureAwait(false);
        if (status.IsFailure || string.Equals(status.Value, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var borrowerLoans = await loans.GetByBorrowerAsync(borrowerId, cancellationToken).ConfigureAwait(false);
        var openLoans = borrowerLoans.Where(l => l.Status == LoanStatus.Active).ToList();
        if (openLoans.Count == 0)
        {
            return;
        }

        var now = clock.UtcNow;
        foreach (var loan in openLoans)
        {
            var flag = LoanReviewFlag.Create(
                loan.Id.Value,
                $"{borrowerType} status changed to '{status.Value}' - review for a recall notice.",
                sourceEventReference,
                now);
            if (flag.IsSuccess)
            {
                reviewFlags.Add(flag.Value);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed record LoanReviewFlagDto(Guid Id, Guid LoanId, string Reason, string SourceEventReference, DateTimeOffset CreatedAt);
