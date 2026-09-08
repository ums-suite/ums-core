using UMS.Modules.Library.Domain.Common;
using UMS.Modules.Library.Domain.Events;
using UMS.Modules.Library.Domain.Fines;

namespace UMS.Modules.Library.UnitTests.Fines;

public sealed class FineTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Fine NewAccruingFine(decimal amount = 10m) =>
        Fine.AccrueNew(Guid.NewGuid(), Guid.NewGuid(), BorrowerType.Student, Money.Create(amount).Value, Now).Value;

    [Fact]
    public void AccrueNew_starts_Accruing_and_raises_FineAccrued()
    {
        var fine = NewAccruingFine();

        Assert.Equal(FineStatus.Accruing, fine.Status);
        Assert.Equal(10m, fine.Amount.Amount);
        Assert.Single(fine.DomainEvents.OfType<FineAccrued>());
    }

    [Fact]
    public void IncreaseAccrual_accumulates_the_running_total()
    {
        var fine = NewAccruingFine();

        var increased = fine.IncreaseAccrual(Money.Create(10m).Value, Now.AddDays(1));

        Assert.True(increased.IsSuccess);
        Assert.Equal(20m, fine.Amount.Amount);
        Assert.Equal(2, fine.DomainEvents.OfType<FineAccrued>().Count());
    }

    [Fact]
    public void IncreaseAccrual_after_settlement_fails_conflict()
    {
        var fine = NewAccruingFine();
        fine.InitiateSettlement(Guid.NewGuid(), Now);
        fine.MarkPaid(Now);

        var result = fine.IncreaseAccrual(Money.Create(10m).Value, Now.AddDays(1));

        Assert.True(result.IsFailure);
        Assert.Equal("fine.not_accruing", result.Error!.Code);
    }

    [Fact]
    public void InitiateSettlement_then_MarkPaid_raises_FineSettled()
    {
        var fine = NewAccruingFine();
        var invoiceId = Guid.NewGuid();

        var initiated = fine.InitiateSettlement(invoiceId, Now);
        var paid = fine.MarkPaid(Now.AddHours(1));

        Assert.True(initiated.IsSuccess);
        Assert.Equal(invoiceId, fine.InvoiceId);
        Assert.True(paid.IsSuccess);
        Assert.Equal(FineStatus.Paid, fine.Status);
        Assert.Single(fine.DomainEvents.OfType<FineSettled>());
    }

    [Fact]
    public void MarkPaid_is_idempotent_against_a_replayed_event()
    {
        var fine = NewAccruingFine();
        fine.InitiateSettlement(Guid.NewGuid(), Now);
        fine.MarkPaid(Now);

        var replayed = fine.MarkPaid(Now);

        Assert.True(replayed.IsSuccess);
        Assert.Equal(FineStatus.Paid, fine.Status);
    }

    [Fact]
    public void Waive_requires_a_non_empty_reason()
    {
        var fine = NewAccruingFine();

        var result = fine.Waive(Guid.NewGuid(), "  ", Now);

        Assert.True(result.IsFailure);
        Assert.Equal("fine.waiver_reason_required", result.Error!.Code);
    }

    [Fact]
    public void Waive_succeeds_and_raises_FineWaived()
    {
        var fine = NewAccruingFine();
        var waivedBy = Guid.NewGuid();

        var result = fine.Waive(waivedBy, "Goodwill gesture", Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(FineStatus.Waived, fine.Status);
        Assert.Equal(waivedBy, fine.WaivedByUserId);
        Assert.Single(fine.DomainEvents.OfType<FineWaived>());
    }

    [Fact]
    public void Waive_an_already_paid_fine_fails_conflict()
    {
        var fine = NewAccruingFine();
        fine.InitiateSettlement(Guid.NewGuid(), Now);
        fine.MarkPaid(Now);

        var result = fine.Waive(Guid.NewGuid(), "Too late", Now);

        Assert.True(result.IsFailure);
        Assert.Equal("fine.already_settled", result.Error!.Code);
    }
}
