using UMS.Modules.Library.Domain.Loans;

namespace UMS.Modules.Library.Application.Abstractions;

/// <summary>LIB-10: the (loan_id, notice_date) unique-constraint backstop's own repository surface (see <see cref="OverdueNotice"/>'s own remarks).</summary>
public interface IOverdueNoticeRepository
{
    public Task<bool> ExistsForLoanAndDateAsync(Guid loanId, DateOnly noticeDate, CancellationToken cancellationToken = default);

    public void Add(OverdueNotice notice);
}
