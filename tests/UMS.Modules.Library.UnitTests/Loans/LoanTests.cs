using UMS.Modules.Library.Domain.Common;
using UMS.Modules.Library.Domain.Events;
using UMS.Modules.Library.Domain.Loans;

namespace UMS.Modules.Library.UnitTests.Loans;

public sealed class LoanTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Loan NewActiveLoan(DateTimeOffset? dueDate = null) =>
        Loan.Issue(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BorrowerType.Student, Guid.NewGuid(), dueDate ?? Now.AddDays(14), Now).Value;

    [Fact]
    public void Issue_with_a_past_due_date_fails_validation()
    {
        var result = Loan.Issue(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BorrowerType.Student, null, Now.AddDays(-1), Now);

        Assert.True(result.IsFailure);
        Assert.Equal("loan.due_date_invalid", result.Error!.Code);
    }

    [Fact]
    public void Issue_raises_LoanIssued_and_starts_Active()
    {
        var loan = NewActiveLoan();

        Assert.Equal(LoanStatus.Active, loan.Status);
        Assert.Single(loan.DomainEvents.OfType<LoanIssued>());
    }

    [Fact]
    public void IsOverdue_is_computed_never_persisted()
    {
        var loan = NewActiveLoan(Now.AddDays(1));

        Assert.False(loan.IsOverdue(Now));
        Assert.True(loan.IsOverdue(Now.AddDays(2)));
    }

    [Fact]
    public void Renew_extends_due_date_and_increments_count()
    {
        var loan = NewActiveLoan();
        var newDueDate = Now.AddDays(28);

        var result = loan.Renew(newDueDate, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(newDueDate, loan.DueDate);
        Assert.Equal(1, loan.RenewalCount);
        Assert.Single(loan.DomainEvents.OfType<LoanRenewed>());
    }

    [Fact]
    public void Renew_with_a_due_date_that_does_not_extend_fails_validation()
    {
        var loan = NewActiveLoan();

        var result = loan.Renew(loan.DueDate.AddDays(-1), Now);

        Assert.True(result.IsFailure);
        Assert.Equal("loan.renewal_due_date_invalid", result.Error!.Code);
    }

    [Fact]
    public void Return_transitions_to_Returned_and_raises_LoanReturned()
    {
        var loan = NewActiveLoan();

        var result = loan.Return(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(LoanStatus.Returned, loan.Status);
        Assert.Single(loan.DomainEvents.OfType<LoanReturned>());
    }

    [Fact]
    public void Return_flags_WasOverdue_correctly()
    {
        var loan = NewActiveLoan(Now.AddDays(1));

        loan.Return(Now.AddDays(5));

        var returned = loan.DomainEvents.OfType<LoanReturned>().Single();
        Assert.True(returned.WasOverdue);
    }

    [Fact]
    public void Renew_after_Return_fails_with_already_returned_conflict()
    {
        var loan = NewActiveLoan();
        loan.Return(Now);

        var result = loan.Renew(Now.AddDays(30), Now);

        Assert.True(result.IsFailure);
        Assert.Equal("loan.already_returned", result.Error!.Code);
    }

    [Fact]
    public void Return_after_Return_fails_with_already_returned_conflict()
    {
        var loan = NewActiveLoan();
        loan.Return(Now);

        var result = loan.Return(Now);

        Assert.True(result.IsFailure);
        Assert.Equal("loan.already_returned", result.Error!.Code);
    }

    [Fact]
    public void ForceCloseAsLost_is_a_distinct_terminal_state_from_Returned()
    {
        var loan = NewActiveLoan();

        var result = loan.ForceCloseAsLost(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(LoanStatus.LostWriteOff, loan.Status);
        Assert.Single(loan.DomainEvents.OfType<LoanLostWriteOff>());

        var returnAttempt = loan.Return(Now);
        Assert.True(returnAttempt.IsFailure);
        Assert.Equal("loan.already_lost_write_off", returnAttempt.Error!.Code);
    }
}
