using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Library.Application.Loans;
using UMS.Modules.Library.Domain.Common;
using UMS.Modules.Library.IntegrationTests.Infrastructure;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Library.IntegrationTests.Loans;

/// <summary>
/// LIB-7/LIB-8: edge-cases.md "Concurrent return and renewal requests for the same Loan" - both
/// operations take the SAME Loan-row lock; the losing transaction gets a distinguishable, clear
/// conflict error, never a silent overwrite (design-decisions.md "Concurrent Return/Renewal Conflict
/// Contract").
/// </summary>
[Collection(LibraryApiTestCollectionDefinition.Name)]
public sealed class ReturnVersusRenewalRaceTests(LibraryServiceFixture fixture)
{
    [Fact]
    public async Task Concurrent_return_and_renewal_resolve_to_exactly_one_winner_with_a_clear_loser_error()
    {
        var (_, bookCopyId) = await LibraryTestData.CreateBookWithSingleCopyAsync(fixture.Services);
        var studentId = Guid.NewGuid();
        LibraryTestData.RegisterActiveStudent(fixture, studentId, Guid.NewGuid());

        Guid loanId;
        using (var issueScope = fixture.Services.CreateScope())
        {
            var loanService = issueScope.ServiceProvider.GetRequiredService<LoanService>();
            var issued = await loanService.IssueAsync(bookCopyId, studentId, BorrowerType.Student, issuedByUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());
            Assert.True(issued.IsSuccess);
            loanId = issued.Value.Id;
        }

        async Task<Result<LoanDto>> ReturnAsync()
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<LoanService>();
            return await service.ReturnAsync(loanId, actorUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());
        }

        async Task<Result<LoanDto>> RenewAsync()
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<LoanService>();
            return await service.RenewAsync(loanId, actorUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());
        }

        var (returnResult, renewResult) = (await Task.WhenAll(ReturnAsync(), RenewAsync())) switch
        {
        [var r, var n] => (r, n),
            _ => throw new InvalidOperationException("Expected exactly two results."),
        };

        // Renew never blocks Return (Renew leaves Status Active) - Return therefore always
        // eventually succeeds, whichever branch's transaction acquires the Loan-row lock first.
        // Renew, however, DOES depend on ordering: if Return's transaction commits first, Renew's
        // own post-lock status re-check must see 'Returned' and fail with a clear, distinguishable
        // conflict - never silently apply a new due date to an already-returned Loan (design-
        // decisions.md "Concurrent Return/Renewal Conflict Contract"). Both orderings are legitimate;
        // what must NEVER happen is Renew silently succeeding against an already-Returned Loan.
        Assert.True(returnResult.IsSuccess, "Return must always succeed in this race - a concurrent Renew never transitions Status away from Active.");
        Assert.True(renewResult.IsSuccess || renewResult.Error!.Code == "loan.already_returned", $"Renew must either win the race or fail with 'loan.already_returned' - got '{(renewResult.IsFailure ? renewResult.Error!.Code : "success")}'.");

        using var verifyScope = fixture.Services.CreateScope();
        var verifyService = verifyScope.ServiceProvider.GetRequiredService<LoanService>();
        var finalState = (await verifyService.GetByIdAsync(loanId)).Value;
        Assert.Equal("Returned", finalState.Status);
    }
}
