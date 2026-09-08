using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Library.Application.Loans;
using UMS.Modules.Library.Domain.Common;
using UMS.Modules.Library.IntegrationTests.Infrastructure;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Library.IntegrationTests.Loans;

/// <summary>
/// LIB-5: edge-cases.md "Oversell under concurrent loan issuance" - a genuine concurrent race (real
/// <see cref="Task.WhenAll"/>, each branch its own DI scope mirroring a distinct HTTP request),
/// not a sequential retry, against the real pessimistic BookCopy row lock + the partial-unique-index
/// backstop (design-decisions.md "Copy-Issuance Concurrency Control Pattern").
/// </summary>
[Collection(LibraryApiTestCollectionDefinition.Name)]
public sealed class LoanIssuanceConcurrencyTests(LibraryServiceFixture fixture)
{
    [Fact]
    public async Task Two_concurrent_issuances_targeting_the_same_last_available_copy_result_in_exactly_one_Loan()
    {
        var (_, bookCopyId) = await LibraryTestData.CreateBookWithSingleCopyAsync(fixture.Services);

        var studentAId = Guid.NewGuid();
        var studentBId = Guid.NewGuid();
        LibraryTestData.RegisterActiveStudent(fixture, studentAId, Guid.NewGuid());
        LibraryTestData.RegisterActiveStudent(fixture, studentBId, Guid.NewGuid());

        async Task<Result<LoanDto>> IssueAsync(Guid borrowerId)
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<LoanService>();
            return await service.IssueAsync(bookCopyId, borrowerId, BorrowerType.Student, issuedByUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());
        }

        var results = await Task.WhenAll(IssueAsync(studentAId), IssueAsync(studentBId));

        Assert.Single(results, r => r.IsSuccess);
        Assert.Single(results, r => r.IsFailure && r.Error!.Code == "book_copy.not_available");
    }

    [Fact]
    public async Task Losing_side_of_the_issuance_race_can_retry_once_the_copy_is_returned()
    {
        var (_, bookCopyId) = await LibraryTestData.CreateBookWithSingleCopyAsync(fixture.Services);

        var studentAId = Guid.NewGuid();
        var studentBId = Guid.NewGuid();
        LibraryTestData.RegisterActiveStudent(fixture, studentAId, Guid.NewGuid());
        LibraryTestData.RegisterActiveStudent(fixture, studentBId, Guid.NewGuid());

        async Task<Result<LoanDto>> IssueAsync(Guid borrowerId)
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<LoanService>();
            return await service.IssueAsync(bookCopyId, borrowerId, BorrowerType.Student, issuedByUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());
        }

        var results = await Task.WhenAll(IssueAsync(studentAId), IssueAsync(studentBId));
        var winnerBorrowerId = results[0].IsSuccess ? studentAId : studentBId;
        var loserBorrowerId = winnerBorrowerId == studentAId ? studentBId : studentAId;

        using var returnScope = fixture.Services.CreateScope();
        var loanService = returnScope.ServiceProvider.GetRequiredService<LoanService>();
        var winnerLoan = (await loanService.GetByBorrowerAsync(winnerBorrowerId)).Single();
        var returned = await loanService.ReturnAsync(winnerLoan.Id, actorUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());
        Assert.True(returned.IsSuccess);

        var retried = await loanService.IssueAsync(bookCopyId, loserBorrowerId, BorrowerType.Student, issuedByUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());
        Assert.True(retried.IsSuccess);
    }
}
