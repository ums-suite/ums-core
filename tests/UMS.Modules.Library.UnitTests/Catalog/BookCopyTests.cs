using UMS.Modules.Library.Domain.Catalog;
using UMS.Modules.Library.Domain.Events;

namespace UMS.Modules.Library.UnitTests.Catalog;

public sealed class BookCopyTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static BookCopy NewAvailableCopy() => BookCopy.Create(BookId.New(), "ACC-0001", "Good", CopyType.Physical, Now).Value;

    [Fact]
    public void New_copy_starts_Available()
    {
        var copy = NewAvailableCopy();

        Assert.Equal(BookCopyStatus.Available, copy.Status);
    }

    [Fact]
    public void MarkOnLoan_from_Available_succeeds()
    {
        var copy = NewAvailableCopy();

        var result = copy.MarkOnLoan();

        Assert.True(result.IsSuccess);
        Assert.Equal(BookCopyStatus.OnLoan, copy.Status);
    }

    [Fact]
    public void MarkOnLoan_from_Reserved_succeeds_for_the_claim_path()
    {
        var copy = NewAvailableCopy();
        copy.MarkReserved();

        var result = copy.MarkOnLoan();

        Assert.True(result.IsSuccess);
        Assert.Equal(BookCopyStatus.OnLoan, copy.Status);
    }

    [Fact]
    public void MarkOnLoan_from_OnLoan_fails_conflict()
    {
        var copy = NewAvailableCopy();
        copy.MarkOnLoan();

        var result = copy.MarkOnLoan();

        Assert.True(result.IsFailure);
        Assert.Equal("book_copy.not_available", result.Error!.Code);
    }

    [Fact]
    public void MarkAvailable_requires_OnLoan()
    {
        var copy = NewAvailableCopy();

        var result = copy.MarkAvailable();

        Assert.True(result.IsFailure);
        Assert.Equal("book_copy.not_on_loan", result.Error!.Code);
    }

    [Fact]
    public void Full_loan_lifecycle_returns_to_Available()
    {
        var copy = NewAvailableCopy();
        copy.MarkOnLoan();

        var returned = copy.MarkAvailable();

        Assert.True(returned.IsSuccess);
        Assert.Equal(BookCopyStatus.Available, copy.Status);
    }

    [Fact]
    public void MarkLost_raises_BookCopyReportedLost_and_is_terminal()
    {
        var copy = NewAvailableCopy();
        copy.MarkOnLoan();

        var lost = copy.MarkLost(Now);

        Assert.True(lost.IsSuccess);
        Assert.Equal(BookCopyStatus.Lost, copy.Status);
        Assert.Single(copy.DomainEvents.OfType<BookCopyReportedLost>());

        var secondAttempt = copy.MarkLost(Now);
        Assert.True(secondAttempt.IsFailure);
        Assert.Equal("book_copy.already_terminal", secondAttempt.Error!.Code);
    }

    [Fact]
    public void ReleaseReservationHold_requires_Reserved()
    {
        var copy = NewAvailableCopy();

        var result = copy.ReleaseReservationHold();

        Assert.True(result.IsFailure);
        Assert.Equal("book_copy.not_reserved", result.Error!.Code);
    }
}
