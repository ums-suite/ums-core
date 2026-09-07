using UMS.Modules.Finance.Domain.Common;
using UMS.Modules.Finance.Domain.Events;
using UMS.Modules.Finance.Domain.FeeStructures;
using UMS.Modules.Finance.Domain.Invoices;

namespace UMS.Modules.Finance.UnitTests.Invoices;

/// <summary>FIN-2: requirement-spec.md §2 Invoice Generation, §4's own invariants.</summary>
public sealed class InvoiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static FeeStructure ActiveFeeStructure(decimal amount = 500m) =>
        FeeStructure.CreateInitialVersion("ApplicationFee", FeeApplicability.ForService("Admission").Value, Money.Create(amount).Value, Now, Now).Value;

    [Theory]
    [InlineData("", "app-1")]
    [InlineData("admission", "")]
    public void Missing_source_module_or_reference_id_is_rejected(string sourceModule, string sourceReferenceId)
    {
        var result = Invoice.Create(sourceModule, sourceReferenceId, "ApplicationFee", OwnerId, ActiveFeeStructure(), Now);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void An_empty_owner_id_is_rejected()
    {
        var result = Invoice.Create("admission", "app-1", "ApplicationFee", Guid.Empty, ActiveFeeStructure(), Now);

        Assert.True(result.IsFailure);
        Assert.Equal("invoice.owner_id_required", result.Error!.Code);
    }

    [Fact]
    public void A_FeeStructure_not_yet_effective_is_rejected()
    {
        var feeStructure = ActiveFeeStructure();

        var result = Invoice.Create("admission", "app-1", "ApplicationFee", OwnerId, feeStructure, Now.AddDays(-1));

        Assert.True(result.IsFailure);
        Assert.Equal("invoice.fee_structure_not_effective", result.Error!.Code);
    }

    [Fact]
    public void A_successfully_created_Invoice_snapshots_the_FeeStructures_amount_and_is_Open()
    {
        var feeStructure = ActiveFeeStructure(750m);

        var invoice = Invoice.Create("admission", "app-1", "ApplicationFee", OwnerId, feeStructure, Now).Value;

        Assert.Equal(InvoiceStatus.Open, invoice.Status);
        Assert.Equal(750m, invoice.TotalAmount.Amount);
        Assert.True(invoice.HasOutstandingBalance);
        Assert.Single(invoice.Items);
        Assert.Equal(feeStructure.VersionNumber, invoice.Items.Single().FeeStructureVersion);
        Assert.Contains(invoice.DomainEvents, e => e is InvoiceGenerated);
    }

    /// <summary>requirement-spec.md §2: a later change to the FeeStructure never retroactively alters this already-generated Invoice.</summary>
    [Fact]
    public void A_later_FeeStructure_version_change_does_not_alter_an_already_generated_Invoice()
    {
        var feeStructure = ActiveFeeStructure(500m);
        var invoice = Invoice.Create("admission", "app-1", "ApplicationFee", OwnerId, feeStructure, Now).Value;

        feeStructure.CreateNewVersion(Money.Create(999m).Value, Now.AddDays(1), Now.AddDays(1));

        Assert.Equal(500m, invoice.TotalAmount.Amount);
    }

    [Fact]
    public void MarkPaid_transitions_an_Open_Invoice_to_Paid()
    {
        var invoice = Invoice.Create("admission", "app-1", "ApplicationFee", OwnerId, ActiveFeeStructure(), Now).Value;

        var result = invoice.MarkPaid(Now.AddMinutes(5));

        Assert.True(result.IsSuccess);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.False(invoice.HasOutstandingBalance);
        Assert.Equal(Now.AddMinutes(5), invoice.PaidAt);
    }

    [Fact]
    public void MarkPaid_against_an_already_Paid_Invoice_is_rejected()
    {
        var invoice = Invoice.Create("admission", "app-1", "ApplicationFee", OwnerId, ActiveFeeStructure(), Now).Value;
        invoice.MarkPaid(Now.AddMinutes(5));

        var result = invoice.MarkPaid(Now.AddMinutes(10));

        Assert.True(result.IsFailure);
        Assert.Equal("invoice.invalid_transition", result.Error!.Code);
    }
}
