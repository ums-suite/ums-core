using Microsoft.EntityFrameworkCore;
using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Domain.Loans;

namespace UMS.Modules.Library.Infrastructure.Persistence.Repositories;

internal sealed class OverdueNoticeRepository(LibraryDbContext context) : IOverdueNoticeRepository
{
    public Task<bool> ExistsForLoanAndDateAsync(Guid loanId, DateOnly noticeDate, CancellationToken cancellationToken = default) =>
        context.OverdueNotices.AnyAsync(n => n.LoanId == loanId && n.NoticeDate == noticeDate, cancellationToken);

    public void Add(OverdueNotice notice) => context.OverdueNotices.Add(notice);
}
