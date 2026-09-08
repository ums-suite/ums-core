namespace UMS.Modules.Library.Application.Loans;

public sealed record LoanDto(
    Guid Id,
    Guid BookCopyId,
    Guid BookId,
    Guid BorrowerId,
    string BorrowerType,
    Guid? IssuedByUserId,
    DateTimeOffset IssuedAt,
    DateTimeOffset DueDate,
    int RenewalCount,
    string Status,
    bool IsOverdue,
    DateTimeOffset? ReturnedAt,
    DateTimeOffset? LostWriteOffAt);

public sealed record IssueLoanRequest(Guid BookCopyId, Guid BorrowerId, string BorrowerType);
