using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Library.Application.Catalog;
using UMS.Modules.Library.Application.Fines;
using UMS.Modules.Library.Application.Loans;
using UMS.Modules.Library.Domain.Common;
using UMS.Modules.Library.IntegrationTests.Infrastructure;

namespace UMS.Modules.Library.IntegrationTests.Catalog;

/// <summary>LIB-17: requirement-spec.md §8 "a BookCopy is reported lost mid-loan → the Loan is force-closed, the copy is marked Lost, and a replacement-cost Fine is generated."</summary>
[Collection(LibraryApiTestCollectionDefinition.Name)]
public sealed class LostCopyWriteOffTests(LibraryServiceFixture fixture)
{
    [Fact]
    public async Task Reporting_a_copy_lost_mid_loan_force_closes_the_Loan_and_generates_a_replacement_Fine()
    {
        var (_, bookCopyId) = await LibraryTestData.CreateBookWithSingleCopyAsync(fixture.Services);
        var studentId = Guid.NewGuid();
        LibraryTestData.RegisterActiveStudent(fixture, studentId, Guid.NewGuid());

        using var scope = fixture.Services.CreateScope();
        var loanService = scope.ServiceProvider.GetRequiredService<LoanService>();
        var issued = await loanService.IssueAsync(bookCopyId, studentId, BorrowerType.Student, issuedByUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());
        Assert.True(issued.IsSuccess);

        var writeOffService = scope.ServiceProvider.GetRequiredService<LostCopyWriteOffService>();
        var writtenOff = await writeOffService.ReportLostAsync(bookCopyId, actorUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());
        Assert.True(writtenOff.IsSuccess);

        var catalogService = scope.ServiceProvider.GetRequiredService<CatalogService>();
        var copies = await catalogService.GetCopiesByBookAsync(issued.Value.BookId);
        var copy = copies.Single(c => c.Id == bookCopyId);
        Assert.Equal("Lost", copy.Status);

        var loanAfter = (await loanService.GetByIdAsync(issued.Value.Id)).Value;
        Assert.Equal("LostWriteOff", loanAfter.Status);

        var fineService = scope.ServiceProvider.GetRequiredService<FineService>();
        var fines = await fineService.GetByBorrowerAsync(studentId);
        Assert.Single(fines);
        Assert.Equal("LostReplacement", fines[0].Reason);
        Assert.Equal(500m, fines[0].Amount);
    }
}
