using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Library.Application.Catalog;
using UMS.Modules.Library.Application.Loans;
using UMS.Modules.Library.Domain.Common;
using UMS.Modules.Library.IntegrationTests.Infrastructure;

namespace UMS.Modules.Library.IntegrationTests.Loans;

/// <summary>LIB-5: design-decisions.md "Fine-Blocks-New-Loan Enforcement Point" and requirement-spec.md §9 decision 2 (max concurrent loans) - both gates evaluated inside the SAME issuance transaction as the BookCopy lock.</summary>
[Collection(LibraryApiTestCollectionDefinition.Name)]
public sealed class BorrowerEligibilityTests(LibraryServiceFixture fixture)
{
    [Fact]
    public async Task A_borrower_with_an_unpaid_Fine_is_rejected_with_a_machine_readable_reason()
    {
        var (_, firstCopyId) = await LibraryTestData.CreateBookWithSingleCopyAsync(fixture.Services);
        var (_, secondCopyId) = await LibraryTestData.CreateBookWithSingleCopyAsync(fixture.Services);
        var studentId = Guid.NewGuid();
        LibraryTestData.RegisterActiveStudent(fixture, studentId, Guid.NewGuid());

        using var scope = fixture.Services.CreateScope();
        var loanService = scope.ServiceProvider.GetRequiredService<LoanService>();
        var issued = await loanService.IssueAsync(firstCopyId, studentId, BorrowerType.Student, issuedByUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());
        Assert.True(issued.IsSuccess);

        var writeOffService = scope.ServiceProvider.GetRequiredService<LostCopyWriteOffService>();
        var writtenOff = await writeOffService.ReportLostAsync(firstCopyId, actorUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());
        Assert.True(writtenOff.IsSuccess);

        var secondAttempt = await loanService.IssueAsync(secondCopyId, studentId, BorrowerType.Student, issuedByUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());

        Assert.True(secondAttempt.IsFailure);
        Assert.Equal("library.unpaid_fine_blocks_loan", secondAttempt.Error!.Code);
    }

    [Fact]
    public async Task A_borrower_may_not_exceed_the_configured_maximum_concurrent_Loan_count()
    {
        var studentId = Guid.NewGuid();
        LibraryTestData.RegisterActiveStudent(fixture, studentId, Guid.NewGuid());

        using var scope = fixture.Services.CreateScope();
        var loanService = scope.ServiceProvider.GetRequiredService<LoanService>();

        // The fixture configures Library:MaxConcurrentLoansStudent = 3.
        for (var i = 0; i < 3; i++)
        {
            var (_, copyId) = await LibraryTestData.CreateBookWithSingleCopyAsync(fixture.Services);
            var issued = await loanService.IssueAsync(copyId, studentId, BorrowerType.Student, issuedByUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());
            Assert.True(issued.IsSuccess);
        }

        var (_, fourthCopyId) = await LibraryTestData.CreateBookWithSingleCopyAsync(fixture.Services);
        var rejected = await loanService.IssueAsync(fourthCopyId, studentId, BorrowerType.Student, issuedByUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());

        Assert.True(rejected.IsFailure);
        Assert.Equal("library.max_concurrent_loans_exceeded", rejected.Error!.Code);
    }

    [Fact]
    public async Task A_reference_only_Category_book_is_never_loan_eligible()
    {
        using var scope = fixture.Services.CreateScope();
        var catalogService = scope.ServiceProvider.GetRequiredService<CatalogService>();
        var category = (await catalogService.CreateCategoryAsync(new CreateCategoryRequest($"Reference-{Guid.NewGuid():N}", IsReferenceOnly: true))).Value;
        var book = (await catalogService.CreateBookAsync(new CreateBookRequest($"Encyclopedia-{Guid.NewGuid():N}", null, category.Id, null, null, IsOpenAccessDigital: false))).Value;
        var copy = (await catalogService.CreateBookCopyAsync(new CreateBookCopyRequest(book.Id, $"REF-{Guid.NewGuid():N}"[..10], "Good", "Physical"))).Value;

        var studentId = Guid.NewGuid();
        LibraryTestData.RegisterActiveStudent(fixture, studentId, Guid.NewGuid());

        var loanService = scope.ServiceProvider.GetRequiredService<LoanService>();
        var result = await loanService.IssueAsync(copy.Id, studentId, BorrowerType.Student, issuedByUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("book.reference_only", result.Error!.Code);
    }
}
