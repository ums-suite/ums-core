using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Library.Application.Fines;
using UMS.Modules.Library.Application.Loans;
using UMS.Modules.Library.Domain.Common;
using UMS.Modules.Library.IntegrationTests.Infrastructure;

namespace UMS.Modules.Library.IntegrationTests.Fines;

/// <summary>LIB-13/LIB-14: requirement-spec.md §2 Fine Accrual (Settlement, waiver) and §4 invariant "a Fine waiver requires an audited actor + reason."</summary>
[Collection(LibraryApiTestCollectionDefinition.Name)]
public sealed class FineSettlementAndWaiverTests(LibraryServiceFixture fixture)
{
    [Fact]
    public async Task Settling_a_Fine_initiates_an_Invoice_and_marks_it_PendingSettlement()
    {
        var (_, bookCopyId) = await LibraryTestData.CreateBookWithSingleCopyAsync(fixture.Services);
        var studentId = Guid.NewGuid();
        var identityUserId = Guid.NewGuid();
        LibraryTestData.RegisterActiveStudent(fixture, studentId, identityUserId);

        using var scope = fixture.Services.CreateScope();
        var loanService = scope.ServiceProvider.GetRequiredService<LoanService>();
        var issued = await loanService.IssueAsync(bookCopyId, studentId, BorrowerType.Student, issuedByUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());
        Assert.True(issued.IsSuccess);

        var writeOffService = scope.ServiceProvider.GetRequiredService<UMS.Modules.Library.Application.Catalog.LostCopyWriteOffService>();
        await writeOffService.ReportLostAsync(bookCopyId, actorUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());

        var fineService = scope.ServiceProvider.GetRequiredService<FineService>();
        var fine = (await fineService.GetByBorrowerAsync(studentId)).Single();

        var settled = await fineService.SettleAsync(fine.Id, actorUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());

        Assert.True(settled.IsSuccess);
        Assert.Equal("PendingSettlement", settled.Value.Status);
        Assert.NotNull(settled.Value.InvoiceId);
    }

    [Fact]
    public async Task Settling_the_same_Fine_twice_fails_conflict()
    {
        var (_, bookCopyId) = await LibraryTestData.CreateBookWithSingleCopyAsync(fixture.Services);
        var studentId = Guid.NewGuid();
        LibraryTestData.RegisterActiveStudent(fixture, studentId, Guid.NewGuid());

        using var scope = fixture.Services.CreateScope();
        var loanService = scope.ServiceProvider.GetRequiredService<LoanService>();
        var issued = await loanService.IssueAsync(bookCopyId, studentId, BorrowerType.Student, issuedByUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());
        var writeOffService = scope.ServiceProvider.GetRequiredService<UMS.Modules.Library.Application.Catalog.LostCopyWriteOffService>();
        await writeOffService.ReportLostAsync(bookCopyId, actorUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());

        var fineService = scope.ServiceProvider.GetRequiredService<FineService>();
        var fine = (await fineService.GetByBorrowerAsync(studentId)).Single();
        await fineService.SettleAsync(fine.Id, actorUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());

        var secondAttempt = await fineService.SettleAsync(fine.Id, actorUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());

        Assert.True(secondAttempt.IsFailure);
        Assert.Equal("fine.not_accruing", secondAttempt.Error!.Code);
    }

    [Fact]
    public async Task Waiving_a_Fine_requires_a_reason_and_records_the_waiving_actor()
    {
        var (_, bookCopyId) = await LibraryTestData.CreateBookWithSingleCopyAsync(fixture.Services);
        var studentId = Guid.NewGuid();
        LibraryTestData.RegisterActiveStudent(fixture, studentId, Guid.NewGuid());

        using var scope = fixture.Services.CreateScope();
        var loanService = scope.ServiceProvider.GetRequiredService<LoanService>();
        var issued = await loanService.IssueAsync(bookCopyId, studentId, BorrowerType.Student, issuedByUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());
        var writeOffService = scope.ServiceProvider.GetRequiredService<UMS.Modules.Library.Application.Catalog.LostCopyWriteOffService>();
        await writeOffService.ReportLostAsync(bookCopyId, actorUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());

        var fineService = scope.ServiceProvider.GetRequiredService<FineService>();
        var fine = (await fineService.GetByBorrowerAsync(studentId)).Single();
        var waivedBy = Guid.NewGuid();

        var missingReason = await fineService.WaiveAsync(fine.Id, waivedBy, string.Empty, Guid.NewGuid().ToString());
        Assert.True(missingReason.IsFailure);

        var waived = await fineService.WaiveAsync(fine.Id, waivedBy, "Damaged in transit, library's fault", Guid.NewGuid().ToString());
        Assert.True(waived.IsSuccess);
        Assert.Equal("Waived", waived.Value.Status);
        Assert.Equal(waivedBy, waived.Value.WaivedByUserId);
    }
}
