using UMS.Modules.Library.Domain.Catalog;

namespace UMS.Modules.Library.UnitTests.Catalog;

public sealed class BookTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_with_blank_title_fails_validation()
    {
        var result = Book.Create("   ", null, null, null, null, isOpenAccessDigital: false, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("book.title_required", result.Error!.Code);
    }

    [Fact]
    public void Create_with_valid_title_succeeds()
    {
        var result = Book.Create("The Bengal Delta", "978-1234567890", null, [Guid.NewGuid()], "2nd", isOpenAccessDigital: false, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal("The Bengal Delta", result.Value.Title);
        Assert.False(result.Value.Withdrawn);
    }

    [Fact]
    public void Withdraw_then_withdraw_again_fails_conflict()
    {
        var book = Book.Create("Title", null, null, null, null, false, Now).Value;

        var first = book.Withdraw(Now);
        var second = book.Withdraw(Now);

        Assert.True(first.IsSuccess);
        Assert.True(book.Withdrawn);
        Assert.True(second.IsFailure);
        Assert.Equal("book.already_withdrawn", second.Error!.Code);
    }

    [Fact]
    public void Reinstate_a_withdrawn_book_clears_the_flag()
    {
        var book = Book.Create("Title", null, null, null, null, false, Now).Value;
        book.Withdraw(Now);

        var reinstated = book.Reinstate();

        Assert.True(reinstated.IsSuccess);
        Assert.False(book.Withdrawn);
        Assert.Null(book.WithdrawnAt);
    }
}
